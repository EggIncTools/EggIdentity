namespace EggIdentity.Settings;

public static class SettingsFrameworkSettings {
    public const string EncryptionKey = "settings.encryption_key";
    public const string EncryptionKeyEnv = "EGGIDENTITY_SETTINGS_KEY";

    public static ISettingsProvider Provider { get; } = new StaticSettingsProvider([
        new SettingDescriptor(
            EncryptionKey, EncryptionKeyEnv, "Settings encryption key", "Core",
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) {
            Description = "Base64 32-byte AES-GCM key protecting stored secrets. Cannot itself be stored.",
        },
    ]);
}
