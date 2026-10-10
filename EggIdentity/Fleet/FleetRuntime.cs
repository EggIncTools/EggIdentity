using EggIdentity.Deploy;
using EggIdentity.Settings.Store;

namespace EggIdentity.Fleet;

public sealed class FleetRuntime(DeployService service, Func<string?> hookSecret, TimeProvider? time = null) {
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RegistryCallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DockerCallTimeout = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _clock = time ?? TimeProvider.System;

    public DeployService Service => service;

    public string? HookSecret => hookSecret();

    public static FleetRuntime Create(SettingsCache cache, HttpMessageHandler? portainerHandler = null, TimeProvider? time = null) {
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
            new DeployEventRing(time: time),
            time,
            () => Live(cache, FleetSettings.RedeployTimeout, TimeSpan.FromMinutes(5)));
        return new FleetRuntime(service, () => cache.Current?.GetString(FleetSettings.HookSecret), time);
    }

    public async Task RunAsync(TimeSpan refreshInterval, CancellationToken ct) {
        if (!service.Configured) return;
        try {
            await RefreshOnceAsync(ct);
            using var timer = new PeriodicTimer(refreshInterval, _clock);
            while (await timer.WaitForNextTickAsync(ct)) await RefreshOnceAsync(ct);
        } catch (OperationCanceledException) {
        }
    }

    private async Task RefreshOnceAsync(CancellationToken ct) {
        try {
            await service.TickAsync(autoDeploy: false, ct);
        } catch (Exception e) when (e is not OperationCanceledException) {
            Console.Error.WriteLine($"fleet: status refresh failed: {e.Message}");
        }
    }

    private static TimeSpan Live(SettingsCache cache, string key, TimeSpan fallback) =>
        cache.Current?.GetDuration(key) is { } value && value > TimeSpan.Zero ? value : fallback;
}
