using EggIdentity.Bot;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Fleet;
using EggIdentity.Settings.Store;

namespace EggIdentity.Host;

public sealed class SuiteBotApps(SettingsCache cache, DeployService fleet) {
    public async Task<IReadOnlyList<SuiteApp>> ServedAsync(CancellationToken ct) =>
        [.. (await cache.GetAsync(ct)).Collection<SuiteApp>(SuiteApps.Key).Where(a => a.Enabled && a.ServedBySuiteBot)];

    public async Task<IReadOnlyList<BotAppSnapshot>> SnapshotsAsync(CancellationToken ct) {
        var result = new List<BotAppSnapshot>();
        foreach (var app in await ServedAsync(ct)) {
            var status = await fleet.StatusAsync(app.Name, ct);
            result.Add(new BotAppSnapshot(app.Name, Snapshot(app, status)));
        }
        return result;
    }

    public static DashboardSnapshot Snapshot(SuiteApp app, DeployStatus? status) {
        ArgumentNullException.ThrowIfNull(app);
        return new DashboardSnapshot {
            AppName = app.Label,
            Version = status?.RunningVersion ?? "",
            BuildHash = status?.RunningRevision ?? "",
            DeployStatus = DeployText(status),
            UptimeSince = status?.StartedAt ?? DateTimeOffset.UnixEpoch,
            RepoUrl = app.RepoUrl ?? "",
        };
    }

    public static string DeployText(DeployStatus? status) {
        if (status is null) return "unknown";
        if (status.Problem is { } problem) return problem;
        if (status.Busy) return "deploying";
        if (status.Running == false) return "stopped";
        if (status.UpdateAvailable) return "update available";
        return status.LastEvent?.Phase.ToString() ?? (status.Running == true ? "running" : "unknown");
    }
}
