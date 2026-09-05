using EggIdentity.Settings;
using EggIdentity.Settings.Store;

namespace EggIdentity.Agent;

public sealed class AppCatalogSync(SettingsCache cache, DeployService service) {
    private SettingsSnapshot? _applied;

    public async Task RunAsync(TimeSpan pollInterval, CancellationToken ct) {
        using var timer = new PeriodicTimer(pollInterval);
        try {
            while (await timer.WaitForNextTickAsync(ct)) {
                try {
                    await SyncOnceAsync(ct);
                } catch (Exception e) when (e is not OperationCanceledException) {
                    Console.Error.WriteLine($"eggidentity-agent: app catalog refresh failed: {e.Message}");
                }
            }
        } catch (OperationCanceledException) {
        }
    }

    public async Task SyncOnceAsync(CancellationToken ct) {
        var snapshot = await cache.GetAsync(ct);
        if (ReferenceEquals(snapshot, _applied)) return;
        _applied = snapshot;

        var diff = service.Apply(AppCatalog.FromSnapshot(snapshot));
        if (diff.IsEmpty) return;
        Console.WriteLine(
            $"eggidentity-agent: apps changed: +{Describe(diff.Added.Select(a => a.Name))} -{Describe(diff.Removed)} ~{Describe(diff.Changed.Select(a => a.Name))}");
    }

    private static string Describe(IEnumerable<string> names) {
        var list = string.Join(",", names);
        return list.Length == 0 ? "none" : list;
    }
}
