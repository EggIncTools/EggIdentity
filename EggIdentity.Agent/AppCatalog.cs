using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Agent;

public sealed record AppCatalogDiff(
    IReadOnlyList<DeployApp> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<DeployApp> Changed) {
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
}

public sealed class AppCatalog {
    private readonly Dictionary<string, DeployApp> _apps;

    public AppCatalog(IEnumerable<DeployApp> apps) {
        ArgumentNullException.ThrowIfNull(apps);
        _apps = new Dictionary<string, DeployApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in apps) {
            if (!app.Enabled || string.IsNullOrWhiteSpace(app.Name)) continue;
            _apps[app.Name] = app;
        }
    }

    public static AppCatalog FromSnapshot(SettingsSnapshot snapshot) {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new AppCatalog(snapshot.Collection<DeployApp>(DeployApps.Key));
    }

    public IReadOnlyDictionary<string, DeployApp> Apps => _apps;

    public bool TryGet(string name, out DeployApp app) {
        if (_apps.TryGetValue(name, out var found)) {
            app = found;
            return true;
        }
        app = null!;
        return false;
    }

    public AppCatalogDiff DiffTo(AppCatalog next) {
        ArgumentNullException.ThrowIfNull(next);
        var added = new List<DeployApp>();
        var changed = new List<DeployApp>();
        foreach (var (name, app) in next._apps) {
            if (!_apps.TryGetValue(name, out var current)) added.Add(app);
            else if (!SameDeployShape(current, app)) changed.Add(app);
        }
        var removed = _apps.Keys.Where(name => !next._apps.ContainsKey(name)).ToList();
        return new AppCatalogDiff(added, removed, changed);
    }

    private static bool SameDeployShape(DeployApp a, DeployApp b) =>
        string.Equals(a.Image, b.Image, StringComparison.Ordinal)
        && string.Equals(a.ContainerName, b.ContainerName, StringComparison.Ordinal)
        && a.AutoDeploy == b.AutoDeploy
        && string.Equals(a.DeploySecret, b.DeploySecret, StringComparison.Ordinal)
        && string.Equals(a.RepoUrl, b.RepoUrl, StringComparison.Ordinal);
}
