using System.IO.Pipelines;
using System.Net;
using EggIdentity.Contract;
using EggIdentity.Settings;

namespace EggIdentity.Deploy.Tests;

public class FleetClientTests {
    private const string Ledger = "eggledger";
    private const string FleetRoot = "/admin/api/fleet/";

    private const string StatusJson = """
        {"app":"eggledger","runningDigest":"sha256:aaa","runningRevision":"abc1234def","runningVersion":"2.3.0",
         "latestDigest":"sha256:bbb","latestRevision":"fff9999","latestVersion":"2.4.0","updateAvailable":true,
         "lastCheckedAt":"2026-09-05T12:00:00+00:00","lastEvent":null,"busy":false,"image":"ghcr.io/x/eggledger:latest",
         "stack":"egg-apps","running":true}
        """;

    [Fact]
    public async Task EveryRequest_CarriesBearerSecret_UnderFleetPrefix() {
        var handler = new FakeFleetHandler((_, _) => FakeFleetHandler.Json(StatusJson));

        await TestFixtures.Client(handler).GetStatusAsync(Ledger, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal(TestFixtures.Secret, request.Headers.Authorization?.Parameter);
        Assert.Equal("http://eggidentity.test:8090/admin/api/fleet/status/eggledger", request.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task GetStatusAsync_ParsesStatus_AndNullOn404() {
        var handler = new FakeFleetHandler((req, _) =>
            req.RequestUri!.AbsolutePath == FleetRoot + "status/eggledger"
                ? FakeFleetHandler.Json(StatusJson)
                : FakeFleetHandler.Text("nope", HttpStatusCode.NotFound));
        var client = TestFixtures.Client(handler);

        var status = await client.GetStatusAsync(Ledger, CancellationToken.None);
        var missing = await client.GetStatusAsync("ghost", CancellationToken.None);

        Assert.NotNull(status);
        Assert.Equal("2.4.0", status.LatestVersion);
        Assert.Equal("egg-apps", status.Stack);
        Assert.True(status.Running);
        Assert.Null(missing);
    }

    [Fact]
    public async Task RestartAsync_ReadsOutcomeBody() {
        var ok = new FakeFleetHandler((_, _) => FakeFleetHandler.Json("""{"ok":true}"""));
        var refused = new FakeFleetHandler((_, _) => FakeFleetHandler.Json("""{"ok":false,"error":"no stack"}"""));
        var unauthorized = new FakeFleetHandler((_, _) => FakeFleetHandler.Text("", HttpStatusCode.Unauthorized));

        Assert.Null(await TestFixtures.Client(ok).RestartAsync(Ledger, CancellationToken.None));
        Assert.Equal("no stack", await TestFixtures.Client(refused).RestartAsync(Ledger, CancellationToken.None));
        Assert.Contains("identity API secret", await TestFixtures.Client(unauthorized).RestartAsync(Ledger, CancellationToken.None), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLogsTailAsync_PassesLines() {
        var handler = new FakeFleetHandler((req, _) => {
            Assert.Equal(FleetRoot + "logs/eggledger", req.RequestUri!.AbsolutePath);
            Assert.Equal("?lines=200", req.RequestUri.Query);
            return FakeFleetHandler.Text("line1\nline2");
        });

        Assert.Equal("line1\nline2", await TestFixtures.Client(handler).GetLogsTailAsync(Ledger, 200, CancellationToken.None));
    }

    [Fact]
    public async Task GetEnvAsync_ParsesLeniently() {
        const string body = """
            [
              {"name":"A","origin":"ServiceEnvironment","masked":true,"value":"***","referenced":false},
              {"name":"B","origin":"envfile"},
              {"name":"C","origin":3},
              {"name":"D"},
              {"origin":"Image"}
            ]
            """;
        var handler = new FakeFleetHandler((_, _) => FakeFleetHandler.Json(body));

        var env = await TestFixtures.Client(handler).GetEnvAsync(Ledger, CancellationToken.None);

        Assert.Equal(["A", "B", "C", "D"], env.Select(e => e.Name));
        Assert.Equal(EnvOrigin.ServiceEnvironment, env[0].Origin);
        Assert.False(env[0].Referenced);
        Assert.Equal(EnvOrigin.EnvFile, env[1].Origin);
        Assert.Equal(EnvOrigin.Image, env[2].Origin);
        Assert.Equal(EnvOrigin.Runtime, env[3].Origin);
    }

    [Fact]
    public async Task PatchStackEnvAsync_SendsJsonBody() {
        var handler = new FakeFleetHandler((req, _) => {
            Assert.Equal(HttpMethod.Patch, req.Method);
            Assert.Equal(FleetRoot + "env/eggledger", req.RequestUri!.AbsolutePath);
            return FakeFleetHandler.Json("""{"ok":true}""");
        });

        var failure = await TestFixtures.Client(handler).PatchStackEnvAsync(
            Ledger, new Dictionary<string, string?> { ["FOO"] = "bar", ["GONE"] = null }, CancellationToken.None);

        Assert.Null(failure);
        var sent = Assert.Single(handler.Bodies);
        Assert.Contains("\"FOO\":\"bar\"", sent, StringComparison.Ordinal);
        Assert.Contains("\"GONE\":null", sent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStacksAsync_ParsesList() {
        const string body = """
            [{"name":"egg-apps","stackId":7,"endpointId":2,"portainerName":"egg-apps","gitBacked":true,
              "repositoryUrl":"https://github.com/EggIncTools/stacks","referenceName":"refs/heads/main",
              "webhookArmed":true,"forceUpdate":true,"refusal":null}]
            """;
        var handler = new FakeFleetHandler((_, _) => FakeFleetHandler.Json(body));

        var stack = Assert.Single(await TestFixtures.Client(handler).GetStacksAsync(CancellationToken.None));

        Assert.Equal(7, stack.StackId);
        Assert.True(stack.Ready);
    }

    [Fact]
    public async Task StreamEventsAsync_SendsReplayHeader_AndYieldsEvents() {
        var pipe = new Pipe();
        var handler = new FakeFleetHandler((req, _) => {
            Assert.Equal(FleetRoot + "events", req.RequestUri!.AbsolutePath);
            Assert.Equal("41", Assert.Single(req.Headers.GetValues("Last-Event-ID")));
            return FakeFleetHandler.Stream(pipe);
        });
        var client = TestFixtures.Client(handler);
        await FakeFleetHandler.WriteAsync(pipe, ": keepalive\n\n");
        await FakeFleetHandler.WriteAsync(pipe, FakeFleetHandler.Frame(TestFixtures.Event(42, phase: DeployPhase.Pulling)));
        await FakeFleetHandler.WriteAsync(pipe, FakeFleetHandler.Frame(TestFixtures.Event(43, phase: DeployPhase.Deployed)));
        await pipe.Writer.CompleteAsync();

        var events = new List<DeployEvent>();
        await foreach (var evt in client.StreamEventsAsync(41, CancellationToken.None)) events.Add(evt);

        Assert.Equal([42L, 43L], events.Select(e => e.Id));
    }

    [Fact]
    public async Task StreamEventsAsync_StalledStream_ThrowsTimeoutAfterIdleWindow() {
        var pipe = new Pipe();
        var handler = new FakeFleetHandler((_, _) => FakeFleetHandler.Stream(pipe));
        var client = TestFixtures.Client(handler, TestFixtures.Options() with { StreamIdleTimeout = TimeSpan.FromMilliseconds(200) });
        await FakeFleetHandler.WriteAsync(pipe, FakeFleetHandler.Frame(TestFixtures.Event(7, phase: DeployPhase.Pulling)));

        var events = new List<DeployEvent>();
        var failure = await Assert.ThrowsAsync<TimeoutException>(async () => {
            await foreach (var evt in client.StreamEventsAsync(null, CancellationToken.None)) events.Add(evt);
        });

        Assert.Equal([7L], events.Select(e => e.Id));
        Assert.Contains("idle", failure.Message, StringComparison.Ordinal);
        await pipe.Writer.CompleteAsync();
    }

    [Fact]
    public void FromEnvironment_NeedsUrlAndSecret() {
        Assert.Null(DeployOptions.FromEnvironment(Ledger, _ => null));
        Assert.Null(DeployOptions.FromEnvironment(Ledger, k => k == DeployOptions.BaseUrlEnv ? "http://eggidentity:8090" : null));

        var options = DeployOptions.FromEnvironment(Ledger, k => k == DeployOptions.BaseUrlEnv ? "http://eggidentity:8090" : "s");

        Assert.Equal("http://eggidentity:8090/admin/api/fleet/", options?.BaseAddress.AbsoluteUri);
    }
}
