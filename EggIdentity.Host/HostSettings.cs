using EggIdentity.Settings;

namespace EggIdentity.Host;

public static class HostSettings {
    private const string Core = "Core";
    private const string Identity = "Identity and SSO";
    private const string Discord = "Discord";
    private const string Sponsors = "Sponsors";
    private const string Storage = "Storage";
    private const string Build = "Build";

    public const string LoginSweepIntervalMinutes = "identity.login_sweep_interval_minutes";
    public const string SponsorTarget = "github.sponsor_target";
    public const string SponsorRoleId = "discord.sponsor_role_id";

    public static ISettingsProvider Provider { get; } = new StaticSettingsProvider([
        new SettingDescriptor(
            "identity.db_connection", "IDENTITY_DB_CONNECTION", "Postgres connection string", Core,
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) {
            Description = "Read before the settings store itself exists, so it can never move into the database.",
            Required = true,
        },
        new SettingDescriptor(
            "identity.api_secret", "IDENTITY_API_SECRET", "Server-to-server API secret", Core,
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) { Required = true },
        new SettingDescriptor(
            "identity.api_port", "IDENTITY_API_PORT", "Listen port", Core,
            SettingKind.Number, ApplyTier.Bootstrap, Sensitivity.Plain) { Default = "8090" },
        new SettingDescriptor(
            "identity.admin_discord_ids", "IDENTITY_ADMIN_DISCORD_IDS", "Admin Discord ids", Core,
            SettingKind.StringList, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "Gates this page. Editing it from here is how you lock yourself out, so it stays on the stack.",
        },
        new SettingDescriptor(
            "settings.encryption_key", "EGGIDENTITY_SETTINGS_KEY", "Settings encryption key", Core,
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) {
            Description = "Base64 32-byte AES-GCM key protecting stored secrets. Cannot itself be stored.",
        },
        new SettingDescriptor(
            LoginSweepIntervalMinutes, "IDENTITY_LOGIN_SWEEP_INTERVAL_MINUTES", "Expired-row sweep interval (minutes)", Core,
            SettingKind.Number, ApplyTier.RestartRequired, Sensitivity.Plain) { Default = "10" },
        new SettingDescriptor(
            "identity.local_key", "EGGIDENTITY_LOCAL_KEY", "Local login key", Identity,
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret),
        new SettingDescriptor(
            "authentik.authority", "AUTHENTIK_AUTHORITY", "Authentik authority", Identity,
            SettingKind.Url, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "Gates the login widget together with the apps directory.",
        },
        new SettingDescriptor(
            "authentik.apps_dir", "AUTHENTIK_APPS_DIR", "Authentik app config directory", Identity,
            SettingKind.Path, ApplyTier.Bootstrap, Sensitivity.Plain),
        new SettingDescriptor(
            "discord.token", "DISCORD_TOKEN", "Bot token", Discord,
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) {
            Description = "Presence of this value is what enables the bot at startup.",
        },
        new SettingDescriptor(
            "discord.guild_id", "DISCORD_GUILD_ID", "Guild id", Discord,
            SettingKind.Snowflake, ApplyTier.RestartRequired, Sensitivity.Plain),
        new SettingDescriptor(
            "bot.config_file", "EGGIDENTITY_BOT_CONFIG_FILE", "Bot config file", Discord,
            SettingKind.Path, ApplyTier.Bootstrap, Sensitivity.Plain) { Default = "/etc/eggidentity/bot.env" },
        new SettingDescriptor(
            "github.sponsor_pat", "GITHUB_SPONSOR_PAT", "GitHub sponsor token", Sponsors,
            SettingKind.Secret, ApplyTier.RestartRequired, Sensitivity.Secret),
        new SettingDescriptor(
            "github.sponsor_webhook_secret", "GITHUB_SPONSOR_WEBHOOK_SECRET", "Sponsor webhook secret", Sponsors,
            SettingKind.Secret, ApplyTier.RestartRequired, Sensitivity.Secret),
        new SettingDescriptor(
            SponsorTarget, "GITHUB_SPONSOR_TARGET", "Sponsor target account", Sponsors,
            SettingKind.Text, ApplyTier.RestartRequired, Sensitivity.Plain),
        new SettingDescriptor(
            SponsorRoleId, "DISCORD_SPONSOR_ROLE_ID", "Sponsor role id", Sponsors,
            SettingKind.Snowflake, ApplyTier.RestartRequired, Sensitivity.Plain),
        new SettingDescriptor(
            "avatar.storage_dir", "AVATAR_STORAGE_DIR", "Avatar storage directory", Storage,
            SettingKind.Path, ApplyTier.Bootstrap, Sensitivity.Plain),
        new SettingDescriptor(
            "build.git_sha", "GIT_SHA", "Build commit", Build,
            SettingKind.ReadOnly, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "Stamped into the image at build time.",
        },
    ]);
}
