using EggIdentity.Deploy;
using EggIdentity.Settings.Store;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Fleet;

public sealed class FleetRuntime(
    DeployService service, Func<string?> hookSecret, TimeProvider? time = null, IUpstreamBuilds? builds = null, PinReconciler? pins = null) {
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RegistryCallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DockerCallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GitHubCallTimeout = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _clock = time ?? TimeProvider.System;

    public DeployService Service => service;

    public string? HookSecret => hookSecret();

    public IUpstreamBuilds? Builds => builds;

    public PinReconciler? Pins => pins;

    public static FleetRuntime Create(
        SettingsCache cache, HttpMessageHandler? portainerHandler = null, TimeProvider? time = null, ILogger? logger = null,
        IUpstreamBuilds? builds = null, IPinStates? pinStates = null, IGitHubRepo? github = null) {
        ArgumentNullException.ThrowIfNull(cache);
        var snapshot = cache.Current ?? throw new InvalidOperationException("load the settings cache before creating the fleet runtime");
        var config = PortainerConfig.FromSnapshot(snapshot);
        var portainer = config is null
            ? null
            : new PortainerApi(config.CreateClient(portainerHandler), config, DockerCallTimeout, () => Live(cache, FleetSettings.PullTimeout, TimeSpan.FromMinutes(10)));
        var service = new DeployService(
            async ct => (await cache.GetAsync(ct)).Collection<SuiteApp>(SuiteApps.Key),
            portainer,
            new RegistryClient(new HttpClient { Timeout = RegistryCallTimeout }),
            new DeployEventRing(time: time, logger: logger),
            time,
            () => Live(cache, FleetSettings.RedeployTimeout, TimeSpan.FromMinutes(5)));
        var pins = builds is null || pinStates is null
            ? null
            : new PinReconciler(
                async ct => {
                    var current = await cache.GetAsync(ct);
                    return (current.Collection<SuiteApp>(SuiteApps.Key),
                        current.Collection<SuiteUpstream>(SuiteUpstreams.UpstreamsKey),
                        current.Collection<AppPin>(SuiteUpstreams.PinsKey));
                },
                github ?? new GitHubRepoClient(new HttpClient { Timeout = GitHubCallTimeout }, () => GitHubToken(cache), () => Committer(cache)),
                builds,
                pinStates,
                () => GitHubToken(cache) is not null,
                service.Events);
        return new FleetRuntime(service, () => cache.Current?.GetString(FleetSettings.HookSecret), time, builds, pins);
    }

    private static string? GitHubToken(SettingsCache cache) =>
        cache.Current?.GetString(FleetSettings.GitHubToken) is { } token && !string.IsNullOrWhiteSpace(token) ? token.Trim() : null;

    private static GitCommitter Committer(SettingsCache cache) => new(
        Trimmed(cache, FleetSettings.CommitterName) ?? GitCommitter.Default.Name,
        Trimmed(cache, FleetSettings.CommitterEmail) ?? GitCommitter.Default.Email);

    private static string? Trimmed(SettingsCache cache, string key) =>
        cache.Current?.GetString(key) is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    public async Task RunAsync(TimeSpan refreshInterval, CancellationToken ct) {
        if (!service.Configured && pins is null) return;
        try {
            await RefreshOnceAsync(ct);
            using var timer = new PeriodicTimer(refreshInterval, _clock);
            while (await timer.WaitForNextTickAsync(ct)) await RefreshOnceAsync(ct);
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            service.Logger?.LogDebug("fleet refresh stopped");
        }
    }

    internal async Task RefreshOnceAsync(CancellationToken ct) {
        if (service.Configured) await DeployTickAsync(ct);
        if (pins is null) return;
        try {
            await pins.ReconcileAsync(null, ct);
        } catch (Exception e) when (!ct.IsCancellationRequested) {
            service.Logger?.LogWarning(e, "pin reconcile failed");
        }
    }

    private async Task DeployTickAsync(CancellationToken ct) {
        try {
            await service.TickAsync(autoDeploy: false, ct);
        } catch (Exception e) when (!ct.IsCancellationRequested) {
            service.Logger?.LogWarning(e, "status refresh failed");
        }
    }

    private static TimeSpan Live(SettingsCache cache, string key, TimeSpan fallback) =>
        cache.Current?.GetDuration(key) is { } value && value > TimeSpan.Zero ? value : fallback;
}
