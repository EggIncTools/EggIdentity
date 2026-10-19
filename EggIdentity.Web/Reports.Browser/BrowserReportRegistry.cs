using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace EggIdentity.Reports.Browser;

public sealed class BrowserReportRegistry : IAsyncDisposable {
    public const string SharedScope = "shared";
    private readonly IServiceProvider _services;
    private readonly BrowserReportOptions _options;
    private readonly ILogger<BrowserReportRegistry> _logger;
    private readonly BrowserWorkerClient _client;
    private readonly Dictionary<string, IReportEngine> _browserEngines = [];
    private Task<ClaimsPrincipal>? _principal;
    private ReportTier? _tier;

    public BrowserReportRegistry(IServiceProvider services, ReportEngineRegistry server, SourceDescriptorCatalog catalog, BrowserReportOptions options, IJSRuntime js, ILogger<BrowserReportRegistry> logger) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(js);
        _services = services;
        _options = options;
        _logger = logger;
        var fallback = new Lazy<ReportEngineRegistry>(services.BuildScopedReportRegistry);
        _client = new BrowserWorkerClient(js, options.RoutePrefix, source => options.SharedSources.Contains(source) ? new ValueTask<string>(SharedScope) : ScopeKeyAsync());
        Registry = new ReportEngineRegistry()
            .SetCaps(ReportTier.Live, server.Caps(ReportTier.Live))
            .SetCaps(ReportTier.Snapshot, server.Caps(ReportTier.Snapshot));
        foreach (var (source, _) in catalog.Descriptors) _browserEngines[source] = new BrowserReportEngine(_client, source);
        foreach (var (source, _) in catalog.Descriptors) {
            var hooks = options.Hooks.GetValueOrDefault(source) ?? new ReportHooks();
            Registry.Add(source, Hooked(hooks, s => fallback.Value.Find(s)));
            Registry.AddSnapshot(source, new SnapshotPassthrough(
                Hooked(hooks, s => fallback.Value.Snapshot(s) ?? fallback.Value.Find(s)),
                catalog.Schemas.GetValueOrDefault(source)));
        }
    }

    public ReportEngineRegistry Registry { get; }

    public bool BrowserActive { get; private set; } = true;

    public ReportTier Tier => _tier ?? throw new InvalidOperationException("await ReadyAsync before reading Tier");

    public async ValueTask<ReportTier> ReadyAsync(CancellationToken ct = default) {
        if (_tier is { } tier) return tier;
        var principal = await PrincipalAsync().WaitAsync(ct).ConfigureAwait(false);
        _tier = _options.Tier(_services, principal);
        return _tier.Value;
    }

    public ValueTask InvalidateAsync(string? source = null, string? partition = null, CancellationToken ct = default) => _client.InvalidateAsync(source, partition, ct);

    public ValueTask ClearBrowserCacheAsync(CancellationToken ct = default) => _client.ClearAsync(ct);

    public ValueTask DisposeAsync() => _client.DisposeAsync();

    private HookedReportEngine Hooked(ReportHooks hooks, Func<string, IReportEngine?> fallback) =>
        new(_browserEngines.GetValueOrDefault, fallback, query => Prepare(query, hooks), hooks.PostProcess, () => BrowserActive, OnFallback);

    private ReportQuery Prepare(ReportQuery query, ReportHooks hooks) {
        var prepared = _options.Prepare?.Invoke(query) ?? query;
        return hooks.Prepare?.Invoke(prepared) ?? prepared;
    }

    private Task<ClaimsPrincipal> PrincipalAsync() => _principal ??= ResolvePrincipalAsync();

    private async Task<ClaimsPrincipal> ResolvePrincipalAsync() {
        if (_services.GetService<AuthenticationStateProvider>() is { } auth) return (await auth.GetAuthenticationStateAsync().ConfigureAwait(false)).User;
        return _services.GetService<IHttpContextAccessor>()?.HttpContext?.User ?? new ClaimsPrincipal();
    }

    private async ValueTask<string> ScopeKeyAsync() {
        var principal = await PrincipalAsync().ConfigureAwait(false);
        return _options.ScopeKey(principal) ?? throw new BrowserUnavailableException("no scope key for this principal; declare the source in SharedSources or sign in");
    }

    private void OnFallback(BrowserUnavailableException reason) {
        if (!BrowserActive) return;
        BrowserActive = false;
        if (reason.Prerender) return;
        _logger.LogWarning(reason, "browser report worker unavailable, this circuit runs reports on the server");
    }

    private sealed class SnapshotPassthrough(IReportEngine inner, SnapshotSchema? schema) : ISnapshotEngine {
        public SnapshotSchema Schema => schema ?? new SnapshotSchema("", [], []);

        public string? Version => null;

        public ReportValidation Validate(ReportQuery query, ReportTierCaps caps) => ReportValidation.Ok;

        public void Invalidate() {
        }

        public Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) => inner.RunAsync(query, context, ct);
    }
}
