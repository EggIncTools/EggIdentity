using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed record SuiteUpstream {
    public string Name { get; init; } = "";
    public string PackagePrefix { get; init; } = "";
    public string? RepoUrl { get; init; }
}

public sealed record AppPin {
    public const string DefaultPropsPath = "Directory.Packages.props";

    public string App { get; init; } = "";
    public string Upstream { get; init; } = "";
    public string Branch { get; init; } = "";
    public string PropsPath { get; init; } = DefaultPropsPath;
    public string? DesiredVersion { get; init; }
    public bool FollowLatest { get; init; }

    public string Id => SuiteUpstreams.PinId(App, Upstream);
}

public static class SuiteUpstreams {
    public const string UpstreamsKey = "suite.upstreams";
    public const string PinsKey = "suite.app_pins";

    public static CollectionDescriptor UpstreamsDescriptor { get; } = new(
        UpstreamsKey, "Suite upstreams", "Suite",
        [
            new FieldDescriptor("name", "Name", SettingKind.Text) {
                Required = true,
                Description = "Must equal the suite.apps name of the app whose CI publishes these packages, for example eggidentity.",
            },
            new FieldDescriptor("package_prefix", "Package prefix", SettingKind.Text) {
                Required = true,
                Description = "Package id prefix, for example EggIdentity.",
            },
            new FieldDescriptor("repo_url", "Repository URL", SettingKind.Url) { Required = true },
        ],
        "name", "name") {
        Description = "One row per package source the suite apps consume.",
    };

    public static CollectionDescriptor PinsDescriptor { get; } = new(
        PinsKey, "App pins", "Suite",
        [
            new FieldDescriptor("id", "Pin id", SettingKind.Text) {
                Required = true,
                Description = "app/upstream, for example eggledger/eggidentity. The upstream part is the upstream's suite.apps name.",
            },
            new FieldDescriptor("app", "App", SettingKind.Text) { Required = true },
            new FieldDescriptor("upstream", "Upstream", SettingKind.Text) { Required = true },
            new FieldDescriptor("branch", "Branch", SettingKind.Text) {
                Required = true,
                Description = "Branch the app deploys from. Pins are read from and committed to it.",
            },
            new FieldDescriptor("props_path", "Props path", SettingKind.Text) { Default = AppPin.DefaultPropsPath },
            new FieldDescriptor("desired_version", "Desired version", SettingKind.Text) {
                Description = "A build such as 142.0.0. Ignored while following latest.",
            },
            new FieldDescriptor("follow_latest", "Follow latest", SettingKind.Bool) {
                Default = "false",
                Description = "Commit every new upstream build. Leave off for the app that hosts this panel.",
            },
        ],
        "id", "id") {
        Description = "One row per app and upstream: which upstream build the app's props file pins.",
    };

    public static ICollectionProvider Provider { get; } = new StaticCollectionProvider([UpstreamsDescriptor, PinsDescriptor]);

    public static string PinId(string app, string upstream) => $"{app}/{upstream}";

    public static IReadOnlyList<string> Problems(AppPin pin, IReadOnlyList<SuiteApp> apps, IReadOnlyList<SuiteUpstream> upstreams) {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentNullException.ThrowIfNull(upstreams);
        var problems = new List<string>();
        var app = apps.FirstOrDefault(a => Same(a.Name, pin.App));
        if (app is null) problems.Add($"No suite app named \"{pin.App}\".");
        else if (string.IsNullOrWhiteSpace(app.RepoUrl)) problems.Add($"\"{pin.App}\" has no repository URL, so its pins cannot be committed.");
        if (!upstreams.Any(u => Same(u.Name, pin.Upstream))) problems.Add($"No upstream named \"{pin.Upstream}\".");
        return problems;
    }

    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
