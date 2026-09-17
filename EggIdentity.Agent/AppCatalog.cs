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
    private readonly Dictionary<string, DeployStack> _stacks;

    public AppCatalog(
        IEnumerable<DeployApp> apps, string environment = DeployApp.ProdEnvironment, IEnumerable<DeployStack>? stacks = null) {
        ArgumentNullException.ThrowIfNull(apps);
        Environment = string.IsNullOrWhiteSpace(environment) ? DeployApp.ProdEnvironment : environment.Trim();
        _apps = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (var app in apps) {
            if (!app.Enabled || string.IsNullOrWhiteSpace(app.Name)) continue;
            if (!string.Equals(app.Environment, Environment, StringComparison.OrdinalIgnoreCase)) continue;
            _apps[app.Name] = app;
        }
        _stacks = [with(StringComparer.OrdinalIgnoreCase)];
        foreach (var stack in stacks ?? []) {
            if (!stack.Enabled || string.IsNullOrWhiteSpace(stack.Name)) continue;
            _stacks[stack.Name] = stack;
        }
    }

    public string Environment { get; }

    public static AppCatalog FromSnapshot(SettingsSnapshot snapshot, string environment = DeployApp.ProdEnvironment) {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new AppCatalog(
            snapshot.Collection<DeployApp>(DeployApps.Key), environment, snapshot.Collection<DeployStack>(DeployStacks.Key));
    }

    public IReadOnlyDictionary<string, DeployApp> Apps => _apps;

    public IReadOnlyDictionary<string, DeployStack> Stacks => _stacks;

    public bool TryGet(string name, out DeployApp app) {
        if (_apps.TryGetValue(name, out var found)) {
            app = found;
            return true;
        }
        app = null!;
        return false;
    }

    public bool TryGetStack(string name, out DeployStack stack) {
        if (!string.IsNullOrEmpty(name) && _stacks.TryGetValue(name, out var found)) {
            stack = found;
            return true;
        }
        stack = null!;
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
        && string.Equals(a.Environment, b.Environment, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.ContainerName, b.ContainerName, StringComparison.Ordinal)
        && a.AutoDeploy == b.AutoDeploy
        && string.Equals(a.DeploySecret, b.DeploySecret, StringComparison.Ordinal)
        && string.Equals(a.RepoUrl, b.RepoUrl, StringComparison.Ordinal)
        && string.Equals(a.Stack, b.Stack, StringComparison.Ordinal);
}
