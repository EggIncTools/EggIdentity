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
    public async Task PinReadsAndWrites_SendSessionHeader() {
        var handler = new FakeFleetHandler((_, _) => FakeFleetHandler.Json("[]"));
        var client = TestFixtures.Client(handler);

        await client.GetPinsAsync("sess-0", CancellationToken.None);
        Assert.Null(await client.SetPinAsync(Ledger, "eggidentity", "142.0.0", false, "sess-1", CancellationToken.None));
        Assert.Null(await client.TakeLatestAsync(null, "sess-2", CancellationToken.None));
        await client.GetBuildsAsync("eggidentity", 20, "sess-3", CancellationToken.None);

        Assert.Equal(4, handler.Requests.Count);
        Assert.Equal(["sess-0"], handler.Requests[0].Headers.GetValues(IdentityWire.SessionHeader));
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
        Assert.Equal(FleetRoot + "pins/eggledger/eggidentity", handler.Requests[1].RequestUri!.AbsolutePath);
        Assert.Equal(["sess-1"], handler.Requests[1].Headers.GetValues(IdentityWire.SessionHeader));
        Assert.Equal(HttpMethod.Post, handler.Requests[2].Method);
        Assert.Equal(FleetRoot + "pins/take-latest", handler.Requests[2].RequestUri!.AbsolutePath);
        Assert.Equal(["sess-2"], handler.Requests[2].Headers.GetValues(IdentityWire.SessionHeader));
        Assert.Equal(["sess-3"], handler.Requests[3].Headers.GetValues(IdentityWire.SessionHeader));
        Assert.Contains("\"desiredVersion\":\"142.0.0\"", handler.Bodies[1], StringComparison.Ordinal);
        Assert.Contains("\"followLatest\":false", handler.Bodies[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPinsAsync_Forbidden_SurfacesAsHttpError() {
        var handler = new FakeFleetHandler((_, _) => FakeFleetHandler.Text("pins need an admin session", HttpStatusCode.Forbidden));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => TestFixtures.Client(handler).GetPinsAsync("user", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task SetPinAsync_ForbiddenBody_IsTheReturnedError() {
        var handler = new FakeFleetHandler((_, _) => FakeFleetHandler.Text("an admin session is required", HttpStatusCode.Forbidden));

        var failure = await TestFixtures.Client(handler).SetPinAsync(Ledger, "EggIdentity", null, true, "sess", CancellationToken.None);

        Assert.Equal("fleet returned 403: your admin session expired or is not an admin session; reload the page", failure);
    }

    [Fact]
    public async Task GetPinsAsync_ParsesViews_WithStringAction() {
        const string body = """
            [{"app":"eggledger","upstream":"EggIdentity","branch":"main","committed":"140.0.0","desired":"142.0.0",
              "followLatest":true,"latest":"142.0.0","pendingVersion":null,"error":null,"action":"Waiting"}]
            """;
        var handler = new FakeFleetHandler((req, _) => {
            Assert.Equal(FleetRoot + "pins", req.RequestUri!.AbsolutePath);
            return FakeFleetHandler.Json(body);
        });

        var pin = Assert.Single(await TestFixtures.Client(handler).GetPinsAsync("sess", CancellationToken.None));

        Assert.Equal(PinAction.Waiting, pin.Action);
        Assert.Equal("main", pin.Branch);
        Assert.Equal("140.0.0", pin.Committed);
        Assert.True(pin.FollowLatest);
    }

    [Fact]
    public async Task GetBuildsAsync_EscapesUpstreamAndPassesLimit() {
        var handler = new FakeFleetHandler((req, _) => {
            Assert.Equal(FleetRoot + "upstreams/Egg%20Identity/builds", req.RequestUri!.AbsolutePath);
            Assert.Equal("?limit=20", req.RequestUri.Query);
            return FakeFleetHandler.Json("""[{"upstream":"Egg Identity","height":142,"sha":"abcdef0123","publishedAt":"2026-10-11T12:00:00+00:00"}]""");
        });

        var build = Assert.Single(await TestFixtures.Client(handler).GetBuildsAsync("Egg Identity", 20, "sess", CancellationToken.None));

        Assert.Equal("142.0.0", build.Version);
        Assert.Equal("abcdef0123", build.Sha);
    }

    [Fact]
    public async Task GetStackServicesAsync_EscapesStackAndParsesServices() {
        var handler = new FakeFleetHandler((req, _) => {
            Assert.Equal(FleetRoot + "portainer/stacks/ei%20servers/services", req.RequestUri!.AbsolutePath);
            return FakeFleetHandler.Json("""[{"service":"db","containerName":"egg-postgres","image":"postgres:17"},{"service":"x"}]""");
        });

        var services = await TestFixtures.Client(handler).GetStackServicesAsync("ei servers", CancellationToken.None);

        Assert.Equal([new StackService("db", "egg-postgres", "postgres:17"), new StackService("x", null, null)], services);
    }

    [Fact]
    public async Task GetPortainerStacksAsync_RefusalSurfacesAsHttpError() {
        var handler = new FakeFleetHandler((req, _) => {
            Assert.Equal(FleetRoot + "portainer/stacks", req.RequestUri!.AbsolutePath);
            return FakeFleetHandler.Text("portainer.api_url is not set", HttpStatusCode.Conflict);
        });

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => TestFixtures.Client(handler).GetPortainerStacksAsync(CancellationToken.None));

        Assert.Contains("portainer.api_url", error.Message, StringComparison.Ordinal);
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
        var time = new ExpiringTimeProvider();
        var handler = new FakeFleetHandler((_, _) => FakeFleetHandler.Stream(pipe));
        var client = TestFixtures.Client(handler, time: time);
        await FakeFleetHandler.WriteAsync(pipe, FakeFleetHandler.Frame(TestFixtures.Event(7, phase: DeployPhase.Pulling)));

        await using var events = client.StreamEventsAsync(null, CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await events.MoveNextAsync());
        Assert.Equal(7L, events.Current.Id);
        time.Expired = true;

        var failure = await Assert.ThrowsAsync<TimeoutException>(async () => await events.MoveNextAsync());

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

    private sealed class ExpiringTimeProvider : TimeProvider {
        public bool Expired { get; set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
            if (Expired) ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
