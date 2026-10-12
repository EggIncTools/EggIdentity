using System.Net;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Fleet.Tests;

public sealed class FleetRoutesHost : IAsyncLifetime {
    public const string HookSecret = "hook-secret";
    public const string Ledger = "eggledger";

    private WebApplication _app = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync() {
        IReadOnlyList<SuiteApp> rows = [new SuiteApp { Name = Ledger, Stack = "egg-apps" }];
        var service = new DeployService(_ => Task.FromResult(rows), null, new NoRegistry(), new DeployEventRing(firstId: 1));
        var runtime = new FleetRuntime(service, () => HookSecret);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var app = _app = builder.Build();
        app.MapGroup("/admin/api").MapFleetAdminApi(runtime);
        app.MapFleetHook(runtime);
        await app.StartAsync();
        Client = app.GetTestClient();
    }

    public async Task DisposeAsync() {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    private sealed class NoRegistry : IImageRegistry {
        public Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) => throw new InvalidOperationException("unused");
    }
}

public class FleetRoutesTests(FleetRoutesHost host) : IClassFixture<FleetRoutesHost> {
    private const string HookSecret = FleetRoutesHost.HookSecret;
    private const string Ledger = FleetRoutesHost.Ledger;

    private static HttpRequestMessage Hook(string app, string? secret, DeployHookPayload? payload = null) {
        var request = new HttpRequestMessage(HttpMethod.Post, FleetRoutes.HookPath) {
            Content = JsonContent.Create(payload ?? new DeployHookPayload(app, "sha256:abc", "rev", "v1")),
        };
        if (secret is not null) request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {secret}");
        return request;
    }

    [Fact]
    public async Task Hook_WrongSecret_IsUnauthorized() {
        var response = await host.Client.SendAsync(Hook(Ledger, "nope"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hook_UnknownApp_IsNotFound() {
        var response = await host.Client.SendAsync(Hook("ghost", HookSecret));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Hook_KnownApp_IsAcceptedAndRecordsRelease() {
        var response = await host.Client.SendAsync(Hook(Ledger, HookSecret));
        var status = await response.Content.ReadFromJsonAsync<DeployStatus>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("sha256:abc", status?.LatestDigest);
    }

    [Fact]
    public async Task Status_ListsEnabledApps() {
        var statuses = await host.Client.GetFromJsonAsync<List<DeployStatus>>("/admin/api/fleet/status");

        Assert.Equal([Ledger], statuses?.Select(s => s.App));
    }

    [Fact]
    public async Task Restart_WithoutPortainer_ReportsRefusal() {
        var response = await host.Client.PostAsync($"/admin/api/fleet/restart/{Ledger}", null);
        var body = await response.Content.ReadFromJsonAsync<AdminSaveResponse>();

        Assert.NotNull(body);
        Assert.False(body.Ok);
        Assert.Contains("portainer.api_url", body.Error, StringComparison.Ordinal);
    }
}

public sealed class FakeUpstreamBuilds : IUpstreamBuilds {
    public List<(string Upstream, long Height, string Sha)> Recorded { get; } = [];

    public Task RecordAsync(string upstream, long height, string sha, CancellationToken ct) {
        Recorded.Add((upstream, height, sha));
        return Task.CompletedTask;
    }

    public Task<UpstreamBuild?> LatestAsync(string upstream, CancellationToken ct) => Task.FromResult<UpstreamBuild?>(null);

    public Task<IReadOnlyList<UpstreamBuild>> ListAsync(string upstream, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<UpstreamBuild>>([]);
}

public sealed class FleetHookUpstreamBuildsTests : IAsyncLifetime {
    private const string HookSecret = FleetRoutesHost.HookSecret;
    private const string Ledger = FleetRoutesHost.Ledger;

    private WebApplication _app = null!;
    private FakeUpstreamBuilds _builds = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync() {
        IReadOnlyList<SuiteApp> rows = [new SuiteApp { Name = Ledger, Stack = "egg-apps" }];
        var service = new DeployService(_ => Task.FromResult(rows), null, new NoRegistry(), new DeployEventRing(firstId: 1));
        _builds = new FakeUpstreamBuilds();
        var runtime = new FleetRuntime(service, () => HookSecret, builds: _builds);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        _app = builder.Build();
        _app.MapFleetHook(runtime);
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync() {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    private static HttpRequestMessage Hook(DeployHookPayload payload) {
        var request = new HttpRequestMessage(HttpMethod.Post, FleetRoutes.HookPath) { Content = JsonContent.Create(payload) };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {HookSecret}");
        return request;
    }

    [Fact]
    public async Task Hook_WithHeightAndPackages_RecordsUpstreamBuild() {
        var payload = new DeployHookPayload(Ledger, "sha256:abc", "rev1", "142.0.0", 142, [new PublishedPackage("EggIdentity.Core", "142.0.0")]);

        await _client.SendAsync(Hook(payload));

        Assert.Equal([(Ledger, 142L, "rev1")], _builds.Recorded);
    }

    [Fact]
    public async Task Hook_OldShapeWithoutPackages_RecordsNothing() {
        var payload = new DeployHookPayload(Ledger, "sha256:abc", "rev1", "v1");

        await _client.SendAsync(Hook(payload));

        Assert.Empty(_builds.Recorded);
    }

    private sealed class NoRegistry : IImageRegistry {
        public Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) => throw new InvalidOperationException("unused");
    }
}

public sealed class FleetPinRoutesTests : IAsyncLifetime, IAsyncDisposable {
    private const string Ledger = FleetRoutesHost.Ledger;
    private const string Upstream = "EggIdentity";
    private const string PinPath = $"/admin/api/fleet/pins/{Ledger}/{Upstream}";

    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private PinRowsAdmin _settings = null!;
    private PinReconciler _pins = null!;

    public async Task InitializeAsync() {
        _settings = new PinRowsAdmin();
        _settings.Put(SuiteApps.Key, Ledger, new Dictionary<string, string?> {
            ["name"] = Ledger,
            ["stack"] = "egg-apps",
            ["repo_url"] = "https://github.com/owner/eggledger",
        });
        _settings.Put(SuiteUpstreams.UpstreamsKey, Upstream, new Dictionary<string, string?> {
            ["name"] = Upstream,
            ["package_prefix"] = Upstream,
            ["repo_url"] = "https://github.com/owner/eggidentity",
        });
        _settings.Put(SuiteUpstreams.PinsKey, SuiteUpstreams.PinId(Ledger, Upstream), new Dictionary<string, string?> {
            ["id"] = SuiteUpstreams.PinId(Ledger, Upstream),
            ["app"] = Ledger,
            ["upstream"] = Upstream,
            ["branch"] = "main",
        });

        IReadOnlyList<SuiteApp> rows = [new SuiteApp { Name = Ledger, Stack = "egg-apps" }];
        var events = new DeployEventRing(firstId: 1);
        var service = new DeployService(_ => Task.FromResult(rows), null, new NoRegistry(), events);
        var builds = new LatestBuilds(new UpstreamBuild(Upstream, 150, "abc", DateTimeOffset.UnixEpoch));
        _pins = new PinReconciler(
            _ => Task.FromResult<(IReadOnlyList<SuiteApp>, IReadOnlyList<SuiteUpstream>, IReadOnlyList<AppPin>)>(
                (_settings.Bound<SuiteApp>(SuiteApps.Key), _settings.Bound<SuiteUpstream>(SuiteUpstreams.UpstreamsKey), _settings.Bound<AppPin>(SuiteUpstreams.PinsKey))),
            new NoGitHub(), builds, new NoPinStates(), () => false, events);
        var runtime = new FleetRuntime(service, () => FleetRoutesHost.HookSecret, builds: builds, pins: _pins);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ISettingsAdmin>(_settings);
        _app = builder.Build();
        _app.MapGroup("/admin/api").MapFleetAdminApi(runtime, (token, _) => Task.FromResult(string.Equals(token, "admin", StringComparison.Ordinal)));
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync() {
        _client.Dispose();
        _pins.Dispose();
        await _app.DisposeAsync();
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await DisposeAsync();

    private static HttpRequestMessage Put(string path, string? session, string? desired = "142.0.0") {
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(new PinWriteRequest(desired, false)) };
        if (session is not null) request.Headers.TryAddWithoutValidation(IdentityWire.SessionHeader, session);
        return request;
    }

    [Fact]
    public async Task Put_WithoutSession_IsForbidden() {
        var response = await _client.SendAsync(Put(PinPath, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(_settings.Row(SuiteUpstreams.PinsKey, SuiteUpstreams.PinId(Ledger, Upstream))?.GetValueOrDefault("desired_version"));
    }

    [Fact]
    public async Task Put_WithUserSession_IsForbidden() {
        var response = await _client.SendAsync(Put(PinPath, "user"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithAdminSession_SavesAndReturnsAppViews() {
        var response = await _client.SendAsync(Put(PinPath, "admin"));
        var views = await response.Content.ReadFromJsonAsync<List<PinView>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("142.0.0", _settings.Row(SuiteUpstreams.PinsKey, SuiteUpstreams.PinId(Ledger, Upstream))?["desired_version"]);
        Assert.Equal([(Ledger, (string?)"142.0.0")], views?.Select(v => (v.App, v.Desired)));
    }

    [Fact]
    public async Task Put_InvalidVersion_IsBadRequest() {
        var response = await _client.SendAsync(Put(PinPath, "admin", "not-a-build"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid version \"not-a-build\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Put_MissingPinRow_IsNotFound() {
        var response = await _client.SendAsync(Put($"/admin/api/fleet/pins/{Ledger}/Other", "admin"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TakeLatest_WithAdminSession_SetsLatestBuild() {
        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/api/fleet/pins/take-latest") { Content = JsonContent.Create(new TakeLatestRequest(Ledger)) };
        request.Headers.TryAddWithoutValidation(IdentityWire.SessionHeader, "admin");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("150.0.0", _settings.Row(SuiteUpstreams.PinsKey, SuiteUpstreams.PinId(Ledger, Upstream))?["desired_version"]);
    }

    [Fact]
    public async Task TakeLatest_WithoutSession_IsForbidden() {
        var response = await _client.PostAsync("/admin/api/fleet/pins/take-latest", JsonContent.Create(new TakeLatestRequest(null)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage Get(string path, string? session) {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (session is not null) request.Headers.TryAddWithoutValidation(IdentityWire.SessionHeader, session);
        return request;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("user")]
    public async Task GetPins_WithoutAdminSession_IsForbidden(string? session) {
        var response = await _client.SendAsync(Get("/admin/api/fleet/pins", session));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPins_WithAdminSession_IsOk() {
        var response = await _client.SendAsync(Get("/admin/api/fleet/pins", "admin"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetBuilds_WithoutSession_IsForbidden() {
        var response = await _client.SendAsync(Get($"/admin/api/fleet/upstreams/{Upstream}/builds", null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBuilds_WithAdminSession_ListsUpstreamBuildViews() {
        var response = await _client.SendAsync(Get($"/admin/api/fleet/upstreams/{Upstream}/builds", "admin"));
        var builds = await response.Content.ReadFromJsonAsync<List<UpstreamBuildView>>();

        Assert.Equal([(Upstream, 150L, "abc")], builds?.Select(b => (b.Upstream, b.Height, b.Sha)));
    }

    private sealed class NoRegistry : IImageRegistry {
        public Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) => throw new InvalidOperationException("unused");
    }

    private sealed class NoGitHub : IGitHubRepo {
        public Task<RepoFile?> GetFileAsync(RepoRef repo, string path, string branch, CancellationToken ct) => throw new InvalidOperationException("unused");

        public Task<RepoCommit> PutFileAsync(RepoRef repo, string path, string branch, string text, string blobSha, string message, CancellationToken ct) =>
            throw new InvalidOperationException("unused");

        public Task<bool> IsAncestorAsync(RepoRef repo, string ancestor, string head, CancellationToken ct) => throw new InvalidOperationException("unused");
    }

    private sealed class NoPinStates : IPinStates {
        public Task<IReadOnlyList<PinState>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<PinState>>([]);

        public Task SetPendingAsync(string app, string upstream, string version, string commit, CancellationToken ct) => Task.CompletedTask;

        public Task ClearPendingAsync(string app, string upstream, CancellationToken ct) => Task.CompletedTask;

        public Task SetErrorAsync(string app, string upstream, string message, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class LatestBuilds(UpstreamBuild latest) : IUpstreamBuilds {
        public Task RecordAsync(string upstream, long height, string sha, CancellationToken ct) => Task.CompletedTask;

        public Task<UpstreamBuild?> LatestAsync(string upstream, CancellationToken ct) =>
            Task.FromResult(string.Equals(upstream, latest.Upstream, StringComparison.OrdinalIgnoreCase) ? latest : null);

        public Task<IReadOnlyList<UpstreamBuild>> ListAsync(string upstream, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UpstreamBuild>>(string.Equals(upstream, latest.Upstream, StringComparison.OrdinalIgnoreCase) ? [latest] : []);
    }

    private sealed class PinRowsAdmin : ISettingsAdmin {
        private readonly Dictionary<(string Key, string Id), Dictionary<string, string?>> _rows = [];

        public string App => "";

        public bool CanRestart => false;

        public void Put(string key, string id, Dictionary<string, string?> values) => _rows[(key, id)] = values;

        public Dictionary<string, string?>? Row(string key, string id) => _rows.GetValueOrDefault((key, id));

        public IReadOnlyList<T> Bound<T>(string key) => [.. _rows.Where(r => string.Equals(r.Key.Key, key, StringComparison.Ordinal)).Select(r => CollectionBinder.Bind<T>(r.Value))];

        public Task<IReadOnlyList<SettingRow>> GetRowsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SettingRow>>([]);

        public Task<IReadOnlyList<CollectionDescriptor>> GetCollectionsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionDescriptor>>([]);

        public Task<IReadOnlyList<CollectionRow>> GetRowsAsync(string collectionKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionRow>>([.. _rows.Where(r => string.Equals(r.Key.Key, collectionKey, StringComparison.Ordinal))
                .Select(r => new CollectionRow(collectionKey, r.Key.Id, r.Value, DateTimeOffset.UnixEpoch, null))]);

        public Task<SettingsSaveResult> SaveAsync(string key, string? value, string? updatedBy, CancellationToken ct = default) =>
            throw new InvalidOperationException("unused");

        public Task<SettingsSaveResult> CreateRowAsync(
            string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy, CancellationToken ct = default) =>
            throw new InvalidOperationException("unused");

        public Task<SettingsSaveResult> SaveRowAsync(
            string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy, CancellationToken ct = default) {
            _rows[(collectionKey, id)] = new Dictionary<string, string?>(values, StringComparer.Ordinal);
            return Task.FromResult(new SettingsSaveResult(true, null, false));
        }

        public Task<SettingsSaveResult> DeleteRowAsync(string collectionKey, string id, CancellationToken ct = default) =>
            throw new InvalidOperationException("unused");

        public Task<DriftReport?> DriftAsync(CancellationToken ct = default) => Task.FromResult<DriftReport?>(null);

        public Task<IReadOnlyList<string>> PendingRestartKeysAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<SettingsSaveResult> RestartAsync(CancellationToken ct = default) => throw new InvalidOperationException("unused");

        public void ClearPendingRestart() {
        }
    }
}

public class FleetPinRoutesUnconfiguredTests(FleetRoutesHost host) : IClassFixture<FleetRoutesHost> {
    [Fact]
    public async Task GetPins_WithoutReconciler_IsConflict() {
        var response = await host.Client.GetAsync("/admin/api/fleet/pins");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("pins not configured on the identity host", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PutPin_WithoutReconciler_IsConflict() {
        var response = await host.Client.PutAsync(
            $"/admin/api/fleet/pins/{FleetRoutesHost.Ledger}/EggIdentity", JsonContent.Create(new PinWriteRequest("142.0.0", false)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetBuilds_WithoutStore_IsConflict() {
        var response = await host.Client.GetAsync("/admin/api/fleet/upstreams/EggIdentity/builds");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

public sealed class ThrowingUpstreamBuilds : IUpstreamBuilds {
    public Task RecordAsync(string upstream, long height, string sha, CancellationToken ct) =>
        throw new InvalidOperationException("db unavailable");

    public Task<UpstreamBuild?> LatestAsync(string upstream, CancellationToken ct) => Task.FromResult<UpstreamBuild?>(null);

    public Task<IReadOnlyList<UpstreamBuild>> ListAsync(string upstream, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<UpstreamBuild>>([]);
}

public sealed class FleetHookUpstreamBuildsFailureTests : IAsyncLifetime {
    private const string HookSecret = FleetRoutesHost.HookSecret;
    private const string Ledger = FleetRoutesHost.Ledger;

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync() {
        IReadOnlyList<SuiteApp> rows = [new SuiteApp { Name = Ledger, Stack = "egg-apps" }];
        var service = new DeployService(_ => Task.FromResult(rows), null, new NoRegistry(), new DeployEventRing(firstId: 1));
        var runtime = new FleetRuntime(service, () => HookSecret, builds: new ThrowingUpstreamBuilds());

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        _app = builder.Build();
        _app.MapFleetHook(runtime);
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync() {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Hook_WhenRecordAsyncThrows_StillReturnsAccepted() {
        var payload = new DeployHookPayload(Ledger, "sha256:abc", "rev1", "142.0.0", 142, [new PublishedPackage("EggIdentity.Core", "142.0.0")]);
        var request = new HttpRequestMessage(HttpMethod.Post, FleetRoutes.HookPath) { Content = JsonContent.Create(payload) };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {HookSecret}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private sealed class NoRegistry : IImageRegistry {
        public Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) => throw new InvalidOperationException("unused");
    }
}
