using EggIdentity.Auth;
using EggIdentity.Config;

namespace EggIdentity.Host;

internal sealed class HostConfig {
    public required string ConnString { get; init; }
    public required string ApiSecret { get; init; }
    public required string Port { get; init; }
    public string? AdminIds { get; init; }
    public int SweepIntervalMinutes { get; init; }

    public string? LocalLoginKey { get; init; }
    public string? AuthentikAuthority { get; init; }
    public string? AuthentikAppsDir { get; init; }
    public bool LoginWidgetEnabled { get; init; }
    public required Dictionary<string, AppAuthConfig> AppConfigs { get; init; }

    public SessionCookieOptions? SessionOptions { get; init; }
    public string? AvatarStorageDir { get; init; }
    public bool ProfileEnabled { get; init; }

    public SponsorConfig? SponsorConfig { get; init; }
    public bool SponsorEnabled { get; init; }

    public required string BotConfigFilePath { get; init; }
    public required IReadOnlyDictionary<string, string> SharedFileValues { get; init; }
    public bool BotEnabled { get; init; }
    public bool AdminEnabled { get; init; }

    public string? SharedFileLookup(string key) => SharedFileValues.GetValueOrDefault(key);

    public static HostConfig FromEnvironment() {
        var connString = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION")
            ?? throw new InvalidOperationException("IDENTITY_DB_CONNECTION is required");
        var apiSecret = Environment.GetEnvironmentVariable("IDENTITY_API_SECRET")
            ?? throw new InvalidOperationException("IDENTITY_API_SECRET is required");

        var authentikAuthority = Environment.GetEnvironmentVariable("AUTHENTIK_AUTHORITY");
        var authentikAppsDir = Environment.GetEnvironmentVariable("AUTHENTIK_APPS_DIR");
        var loginWidgetEnabled = !string.IsNullOrEmpty(authentikAuthority) && !string.IsNullOrEmpty(authentikAppsDir);

        var sessionOptions = SessionCookieOptions.FromEnvironment();
        var avatarStorageDir = Environment.GetEnvironmentVariable("AVATAR_STORAGE_DIR");
        var sponsorConfig = SponsorConfig.FromEnvironment();

        var botConfigFilePath = Environment.GetEnvironmentVariable("EGGIDENTITY_BOT_CONFIG_FILE")
            ?? "/etc/eggidentity/bot.env";
        var botEnabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISCORD_TOKEN"));

        return new HostConfig {
            ConnString = connString,
            ApiSecret = apiSecret,
            Port = Environment.GetEnvironmentVariable("IDENTITY_API_PORT") ?? "8090",
            AdminIds = Environment.GetEnvironmentVariable("IDENTITY_ADMIN_DISCORD_IDS"),
            SweepIntervalMinutes =
                int.TryParse(Environment.GetEnvironmentVariable("IDENTITY_LOGIN_SWEEP_INTERVAL_MINUTES"), out var m) ? m : 10,
            LocalLoginKey = Environment.GetEnvironmentVariable("EGGIDENTITY_LOCAL_KEY"),
            AuthentikAuthority = authentikAuthority,
            AuthentikAppsDir = authentikAppsDir,
            LoginWidgetEnabled = loginWidgetEnabled,
            AppConfigs = loginWidgetEnabled
                ? AppAuthConfigLoader.LoadFromDirectory(authentikAppsDir!, authentikAuthority!)
                : [],
            SessionOptions = sessionOptions,
            AvatarStorageDir = avatarStorageDir,
            ProfileEnabled = loginWidgetEnabled && sessionOptions is not null && !string.IsNullOrEmpty(avatarStorageDir),
            SponsorConfig = sponsorConfig,
            SponsorEnabled = sponsorConfig is not null && sessionOptions is not null,
            BotConfigFilePath = botConfigFilePath,
            SharedFileValues = BotConfigLoader.ParseFile(botConfigFilePath),
            BotEnabled = botEnabled,
            AdminEnabled = botEnabled && loginWidgetEnabled && sessionOptions is not null,
        };
    }
}
