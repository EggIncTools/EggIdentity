using System.IO.Pipelines;
using System.Net;
using System.Security.Claims;
using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.Settings;

namespace EggIdentity.Deploy.Tests;

public class AgentClientTests {
    private const string StatusJson = """
        {"app":"eggledger","runningDigest":"sha256:aaa","runningRevision":"abc1234def","runningVersion":"2.3.0",
         "latestDigest":"sha256:bbb","latestRevision":"fff9999","latestVersion":"2.4.0","updateAvailable":true,
         "lastCheckedAt":"2026-09-05T12:00:00+00:00","lastEvent":null,"busy":false}
        """;

    [Fact]
    public async Task EveryRequest_CarriesAdminSessionCookie() {
        var handler = new FakeAgentHandler((_, _) => FakeAgentHandler.Json(StatusJson));
        var client = TestFixtures.Client(handler);

        await client.GetStatusAsync("eggledger", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        var cookie = Assert.Single(request.Headers.GetValues("Cookie"));
        Assert.StartsWith(TestFixtures.Session.CookieName + "=", cookie, StringComparison.Ordinal);
        var token = cookie[(TestFixtures.Session.CookieName.Length + 1)..];
        var principal = SessionToken.Validate(TestFixtures.Session, token, DateTimeOffset.UtcNow);
        Assert.NotNull(principal);
        Assert.True(principal.IsAtLeast(UserRole.Admin));
        Assert.Equal("eggledger", principal.FindFirst("sub")?.Value);
    }

    [Fact]
    public async Task GetStatusAsync_ParsesStatus_AndNullOn404() {
        var handler = new FakeAgentHandler((req, _) =>
            req.RequestUri!.AbsolutePath == "/status/eggledger" ? FakeAgentHandler.Json(StatusJson) : FakeAgentHandler.Text("nope", HttpStatusCode.NotFound));
        var client = TestFixtures.Client(handler);

        var status = await client.GetStatusAsync("eggledger", CancellationToken.None);
        var missing = await client.GetStatusAsync("ghost", CancellationToken.None);

        Assert.NotNull(status);
        Assert.Equal("2.3.0", status.RunningVersion);
        Assert.Equal("2.4.0", status.LatestVersion);
        Assert.True(status.UpdateAvailable);
        Assert.Null(missing);
    }

    [Fact]
    public async Task DeployAsync_Reads202Body() {
        var handler = new FakeAgentHandler((req, _) => {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/deploy/eggledger", req.RequestUri!.AbsolutePath);
            return FakeAgentHandler.Json(StatusJson, HttpStatusCode.Accepted);
        });

        var status = await TestFixtures.Client(handler).DeployAsync("eggledger", CancellationToken.None);

        Assert.Equal("eggledger", status.App);
    }

    [Fact]
    public async Task RestartAsync_ReturnsNullOnSuccess_AndFailureText() {
        var ok = new FakeAgentHandler((_, _) => FakeAgentHandler.Json(StatusJson));
        var bad = new FakeAgentHandler((_, _) => FakeAgentHandler.Text("engine down", HttpStatusCode.BadGateway));

        Assert.Null(await TestFixtures.Client(ok).RestartAsync("eggledger", CancellationToken.None));
        Assert.Equal("agent returned 502: engine down", await TestFixtures.Client(bad).RestartAsync("eggledger", CancellationToken.None));
    }

    [Fact]
    public async Task GetLogsTailAsync_PassesLinesAndReturnsText() {
        var handler = new FakeAgentHandler((req, _) => {
            Assert.Equal("/logs/eggledger/tail", req.RequestUri!.AbsolutePath);
            Assert.Equal("?lines=200", req.RequestUri.Query);
            return FakeAgentHandler.Text("line1\nline2");
        });

        var text = await TestFixtures.Client(handler).GetLogsTailAsync("eggledger", 200, CancellationToken.None);

        Assert.Equal("line1\nline2", text);
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
        var handler = new FakeAgentHandler((_, _) => FakeAgentHandler.Json(body));

        var env = await TestFixtures.Client(handler).GetEnvAsync("eggledger", CancellationToken.None);

        Assert.Equal(["A", "B", "C", "D"], env.Select(e => e.Name));
        Assert.Equal(EnvOrigin.ServiceEnvironment, env[0].Origin);
        Assert.True(env[0].Masked);
        Assert.Equal("***", env[0].Value);
        Assert.False(env[0].Referenced);
        Assert.Equal(EnvOrigin.EnvFile, env[1].Origin);
        Assert.True(env[1].Referenced);
        Assert.Equal(EnvOrigin.Image, env[2].Origin);
        Assert.Equal(EnvOrigin.Runtime, env[3].Origin);
        Assert.False(env[3].Masked);
    }

    [Fact]
    public async Task PatchStackEnvAsync_SendsJsonBody() {
        var handler = new FakeAgentHandler((req, _) => {
            Assert.Equal(HttpMethod.Patch, req.Method);
            Assert.Equal("/stack/env", req.RequestUri!.AbsolutePath);
            return FakeAgentHandler.Json("{\"updated\":2}");
        });

        var failure = await TestFixtures.Client(handler).PatchStackEnvAsync(
            new Dictionary<string, string?> { ["FOO"] = "bar", ["GONE"] = null }, CancellationToken.None);

        Assert.Null(failure);
        var sent = Assert.Single(handler.Bodies);
        Assert.NotNull(sent);
        Assert.Contains("\"FOO\":\"bar\"", sent, StringComparison.Ordinal);
        Assert.Contains("\"GONE\":null", sent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReconcileStackAsync_PostsAndReportsFailure() {
        var handler = new FakeAgentHandler((req, _) => {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/stack/reconcile", req.RequestUri!.AbsolutePath);
            return FakeAgentHandler.Text("portainer is not configured", HttpStatusCode.ServiceUnavailable);
        });

        var failure = await TestFixtures.Client(handler).ReconcileStackAsync(CancellationToken.None);

        Assert.Equal("agent returned 503: portainer is not configured", failure);
    }

    [Fact]
    public async Task StreamEventsAsync_SendsReplayHeader_AndYieldsEvents() {
        var pipe = new Pipe();
        var handler = new FakeAgentHandler((req, _) => {
            Assert.Equal("/events", req.RequestUri!.AbsolutePath);
            Assert.Equal("41", Assert.Single(req.Headers.GetValues("Last-Event-ID")));
            return FakeAgentHandler.Stream(pipe);
        });
        var client = TestFixtures.Client(handler);
        await FakeAgentHandler.WriteAsync(pipe, ": keepalive\n\n");
        await FakeAgentHandler.WriteAsync(pipe, FakeAgentHandler.Frame(TestFixtures.Event(42, phase: DeployPhase.Pulling)));
        await FakeAgentHandler.WriteAsync(pipe, FakeAgentHandler.Frame(TestFixtures.Event(43, phase: DeployPhase.Deployed)));
        await pipe.Writer.CompleteAsync();

        var events = new List<DeployEvent>();
        await foreach (var evt in client.StreamEventsAsync(41, CancellationToken.None)) events.Add(evt);

        Assert.Equal([42L, 43L], events.Select(e => e.Id));
        Assert.Equal(DeployPhase.Deployed, events[1].Phase);
    }

    [Fact]
    public async Task StreamEventsAsync_StalledStream_ThrowsTimeoutAfterIdleWindow() {
        var pipe = new Pipe();
        var handler = new FakeAgentHandler((_, _) => FakeAgentHandler.Stream(pipe));
        var options = TestFixtures.Options() with { StreamIdleTimeout = TimeSpan.FromMilliseconds(200) };
        var client = TestFixtures.Client(handler, options);
        await FakeAgentHandler.WriteAsync(pipe, FakeAgentHandler.Frame(TestFixtures.Event(7, phase: DeployPhase.Pulling)));

        var events = new List<DeployEvent>();
        var failure = await Assert.ThrowsAsync<TimeoutException>(async () => {
            await foreach (var evt in client.StreamEventsAsync(null, CancellationToken.None)) events.Add(evt);
        });

        Assert.Equal([7L], events.Select(e => e.Id));
        Assert.Contains("idle", failure.Message, StringComparison.Ordinal);
        await pipe.Writer.CompleteAsync();
    }

    [Fact]
    public async Task StreamEventsAsync_CallerCancellation_IsNotReportedAsIdleTimeout() {
        var pipe = new Pipe();
        var handler = new FakeAgentHandler((_, _) => FakeAgentHandler.Stream(pipe));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var client = TestFixtures.Client(handler, TestFixtures.Options() with { StreamIdleTimeout = TimeSpan.FromSeconds(30) });

        var events = new List<DeployEvent>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            await foreach (var evt in client.StreamEventsAsync(null, cts.Token)) events.Add(evt);
        });

        Assert.Empty(events);
        await pipe.Writer.CompleteAsync();
    }

    [Fact]
    public async Task StreamEventsAsync_WithoutAfterId_OmitsReplayHeader() {
        var pipe = new Pipe();
        var handler = new FakeAgentHandler((req, _) => {
            Assert.False(req.Headers.Contains("Last-Event-ID"));
            return FakeAgentHandler.Stream(pipe);
        });
        await pipe.Writer.CompleteAsync();

        var events = new List<DeployEvent>();
        await foreach (var evt in TestFixtures.Client(handler).StreamEventsAsync(null, CancellationToken.None)) events.Add(evt);

        Assert.Empty(events);
    }
}
