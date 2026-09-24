using System.Diagnostics.CodeAnalysis;

namespace EggIdentity.Settings.Api;

public sealed record AdminTargetRow {
    public string Name { get; init; } = "";
    public string AdminBaseUrl { get; init; } = "";
    public bool Enabled { get; init; } = true;

    public bool IsUsable => !string.IsNullOrWhiteSpace(Name) && TryUrl(AdminBaseUrl, out _);

    internal static bool TryUrl(string value, [NotNullWhen(true)] out Uri? url) {
        url = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;
        url = parsed;
        return true;
    }
}

public static class AdminTargets {
    public const string Key = "admin.targets";
    public const string SecretEnvPrefix = "ADMIN_SECRET_";

    public static CollectionDescriptor Descriptor { get; } = new(
        Key, "Admin targets", "Deploy",
        [
            new FieldDescriptor("name", "App name", SettingKind.Text) {
                Required = true,
                Description = "Joins to the app's deploy.apps row and names its secret environment variable.",
            },
            new FieldDescriptor("admin_base_url", "Admin base URL", SettingKind.Url) {
                Required = true,
                Description = "Internal address serving /admin/api, for example http://eggledger:5015. Not the public URL.",
            },
            new FieldDescriptor("enabled", "Enabled", SettingKind.Bool) {
                Default = "true",
                Description = "Off hides the app from the admin pane without losing its address.",
            },
        ],
        "name", "name") {
        Description = "One row per app administrable from the hub. Each secret is read from "
            + SecretEnvPrefix + "<NAME> in the hub's environment, never stored here.",
    };

    public static ICollectionProvider Provider { get; } = new StaticCollectionProvider([Descriptor]);

    public static string SecretEnvKey(string app) {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);
        var name = new string([.. app.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')]);
        return SecretEnvPrefix + name;
    }

    public static AdminTarget? Resolve(AdminTargetRow row, Func<string, string?> environment) =>
        Describe(row, environment).Target;

    public static AdminTargetStatus Describe(AdminTargetRow row, Func<string, string?> environment) {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(environment);

        var envKey = string.IsNullOrWhiteSpace(row.Name) ? SecretEnvPrefix : SecretEnvKey(row.Name);
        if (string.IsNullOrWhiteSpace(row.Name))
            return new AdminTargetStatus(row.Name, envKey, false, "this row has no app name");
        if (!row.Enabled)
            return new AdminTargetStatus(row.Name, envKey, false, "disabled in admin.targets");
        if (!AdminTargetRow.TryUrl(row.AdminBaseUrl, out var url))
            return new AdminTargetStatus(row.Name, envKey, false, "admin base URL is not an absolute URL");

        var secret = environment(envKey);
        if (string.IsNullOrWhiteSpace(secret))
            return new AdminTargetStatus(row.Name, envKey, false, $"{envKey} is not set on this host");

        return new AdminTargetStatus(row.Name, envKey, true, null) {
            Target = new AdminTarget(row.Name, url, secret),
        };
    }
}

public sealed record AdminTargetStatus(string App, string SecretEnvKey, bool SecretPresent, string? Unavailable) {
    public AdminTarget? Target { get; init; }

    public bool Administrable => Target is not null;
}
