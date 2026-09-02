using EggIdentity.Settings;

namespace EggIdentity.Auth;

public static class SessionSettings {
    public const string Secret = "session.secret";
    public const string SecretPrevious = "session.secret_previous";
    public const string TtlMinutes = "session.ttl_minutes";
    public const string CookieDomain = "session.cookie_domain";

    private const string Category = "Identity and SSO";

    public static ISettingsProvider Provider { get; } = new StaticSettingsProvider([
        new SettingDescriptor(
            Secret, "EGGIDENTITY_SESSION_SECRET", "Session signing secret", Category,
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) {
            Description = "HS256 key for the shared parent-domain session cookie. Rotating it signs out every app.",
            Required = true,
        },
        new SettingDescriptor(
            SecretPrevious, "EGGIDENTITY_SESSION_SECRET_PREVIOUS", "Previous session secret", Category,
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) {
            Description = "Accepted during rotation so existing cookies stay valid.",
        },
        new SettingDescriptor(
            TtlMinutes, "EGGIDENTITY_SESSION_TTL_MINUTES", "Session lifetime (minutes)", Category,
            SettingKind.Number, ApplyTier.RestartRequired, Sensitivity.Plain) {
            Default = "43200",
        },
        new SettingDescriptor(
            CookieDomain, "EGGIDENTITY_SESSION_COOKIE_DOMAIN", "Session cookie domain", Category,
            SettingKind.Text, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "Parent domain the session cookie is scoped to, shared by every consuming app.",
        },
    ]);
}
