using EggIdentity.Resilience;
using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed record DeployOptions(string BaseUrl, string AppName, string Secret) {
    public const string HttpClientName = "eggidentity-fleet";
    public const string BaseUrlEnv = "IDENTITY_API_URL";
    public const string SecretEnv = "IDENTITY_API_SECRET";
    public const string BaseUrlKey = "identity.api_url";
    public const string SecretKey = "identity.api_secret";

    public static ISettingsProvider Provider { get; } = SharedProviders.Register(new StaticSettingsProvider([
        new SettingDescriptor(
            BaseUrlKey, BaseUrlEnv, "Identity API URL", "Identity: SSO",
            SettingKind.Url, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "Base URL of the identity host, used for fleet, deploy and Discord registration calls.",
        },
        new SettingDescriptor(
            SecretKey, SecretEnv, "Identity API secret", "Identity: SSO",
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) {
            Description = "Bearer for the identity host's server-to-server API.",
        },
    ]));
    public const string FleetPrefix = "admin/api/fleet/";
    public static readonly TimeSpan MaxReconnectCeiling = TimeSpan.FromSeconds(60);

    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxReconnectDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan CallTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan StreamIdleTimeout { get; init; } = TimeSpan.FromSeconds(45);

    public RetryOptions ReconnectRetry => new() {
        MaxAttempts = int.MaxValue,
        BaseDelay = ReconnectDelay,
        MaxDelay = MaxReconnectDelay < MaxReconnectCeiling ? MaxReconnectDelay : MaxReconnectCeiling,
    };

    public Uri BaseAddress => new((BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/") + FleetPrefix, UriKind.Absolute);

    public static DeployOptions? FromEnvironment(string appName, Func<string, string?>? environment = null) {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        var env = environment ?? Environment.GetEnvironmentVariable;
        var baseUrl = env(BaseUrlEnv);
        var secret = env(SecretEnv);
        return string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret)
            ? null
            : new DeployOptions(baseUrl.Trim(), appName, secret);
    }
}
