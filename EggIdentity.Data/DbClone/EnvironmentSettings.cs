using EggIdentity.Settings;

namespace EggIdentity.DbClone;

public static class EnvironmentSettings {
    public const string Key = "app.environment";
    public const string EnvKey = "APP_ENVIRONMENT";
    public const string Prod = "prod";
    public const string SubProd = "subprod";
    public const string InstanceKey = "app.instance";
    public const string InstanceEnvKey = "APP_INSTANCE";
    public const string DiscordGate = "discord";

    public static ISettingsProvider Provider { get; } = SharedProviders.Register(new StaticSettingsProvider([
        new SettingDescriptor(
            Key, EnvKey, "Environment", "Deploy",
            SettingKind.Enum, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Default = Prod,
            EnumValues = [Prod, SubProd],
            Description = "Which environment this process is. Sub-prod may clone production data onto its own database; production never can.",
        },
        new SettingDescriptor(
            InstanceKey, InstanceEnvKey, "Instance name", "Deploy",
            SettingKind.Text, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "This instance's suite.apps name. Defaults to the app's prod name, or <name>-subprod in sub-prod.",
        },
    ]));

    public static string Resolve(Func<string, string?> environment) {
        ArgumentNullException.ThrowIfNull(environment);
        var value = environment(EnvKey);
        return string.IsNullOrWhiteSpace(value) ? Prod : value.Trim();
    }

    public static string Instance(Func<string, string?> environment, string prodName) {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(prodName);
        var value = environment(InstanceEnvKey);
        if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        return IsSubProd(Resolve(environment)) ? $"{prodName}-{SubProd}" : prodName;
    }

    public static bool DiscordAllowed(Func<string, string?> environment) {
        ArgumentNullException.ThrowIfNull(environment);
        return !IsSubProd(Resolve(environment)) || SubProdFence.IsOpen(new FenceGate(DiscordGate, []), environment);
    }

    public static bool IsSubProd(string? value) =>
        string.Equals(value?.Trim(), SubProd, StringComparison.OrdinalIgnoreCase);
}
