using EggIdentity.Settings;

namespace EggIdentity.DbClone;

public static class EnvironmentSettings {
    public const string Key = "app.environment";
    public const string EnvKey = "APP_ENVIRONMENT";
    public const string Prod = "prod";
    public const string SubProd = "subprod";

    public static ISettingsProvider Provider { get; } = new StaticSettingsProvider([
        new SettingDescriptor(
            Key, EnvKey, "Environment", "Deploy",
            SettingKind.Enum, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Default = Prod,
            EnumValues = [Prod, SubProd],
            Description = "Which environment this process is. Sub-prod may clone production data onto its own database; production never can.",
        },
    ]);

    public static string Resolve(Func<string, string?> environment) {
        ArgumentNullException.ThrowIfNull(environment);
        var value = environment(EnvKey);
        return string.IsNullOrWhiteSpace(value) ? Prod : value.Trim();
    }

    public static bool IsSubProd(string? value) =>
        string.Equals(value?.Trim(), SubProd, StringComparison.OrdinalIgnoreCase);
}
