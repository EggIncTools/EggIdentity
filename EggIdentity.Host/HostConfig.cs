using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.DbClone;
using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Host;

internal sealed record HostConfig {
    public required string ConnString { get; init; }
    public required string ApiSecret { get; init; }
    public required string Port { get; init; }
    public string? AdminIds { get; init; }
    public int SweepIntervalMinutes { get; init; } = 10;
    public int ReconcileIntervalMinutes { get; init; } = 30;

    public string? LocalLoginKey { get; init; }
    public string? AuthentikAuthority { get; init; }
    public string? AuthentikTokenDecryptionKey { get; init; }
    public string? AuthentikApiToken { get; init; }
    public bool LoginWidgetEnabled { get; init; }

    public SessionCookieOptions? SessionOptions { get; init; }
    public string? AvatarStorageDir { get; init; }
    public bool ProfileEnabled { get; init; }

    public SponsorConfig? SponsorConfig { get; init; }
    public bool SponsorEnabled { get; init; }

    public DiscordRegistrationResponse? DiscordRegistration { get; init; }
    public string? RepoUrl { get; init; }
    public bool BotEnabled => DiscordRegistration is not null;

    public HostConfig WithSettings(SettingsSnapshot snapshot, Func<string, string?> environment) {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(environment);
        var self = snapshot.Collection<SuiteApp>(SuiteApps.Key).FirstOrDefault(a =>
            string.Equals(a.Name, EnvironmentSettings.Instance(environment, HostServices.AppName), StringComparison.OrdinalIgnoreCase));
        var registration = EnvironmentSettings.DiscordAllowed(environment) ? DiscordRegistrationRoutes.ToResponse(self) : null;
        var sponsor = SponsorConfig.From(snapshot, registration);
        var sweep = snapshot.GetInt(HostSettings.LoginSweepIntervalMinutes, 10);
        var reconcile = snapshot.GetInt(HostSettings.ReconcileIntervalMinutes, 30);
        return this with {
            SweepIntervalMinutes = sweep > 0 ? sweep : 10,
            ReconcileIntervalMinutes = reconcile > 0 ? reconcile : 30,
            DiscordRegistration = registration,
            RepoUrl = self?.RepoUrl,
            SponsorConfig = sponsor,
            SponsorEnabled = sponsor is not null && SessionOptions is not null,
        };
    }

    private static string? ReadPemOrFile(string envKey, string? value) {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Contains("-----BEGIN", StringComparison.Ordinal)) return value;
        return SettingsValidation.ValidateKind(SettingKind.Path, value, []) is string error
            ? throw new InvalidOperationException($"{envKey} is not PEM text, and as a path {error}: \"{value}\"")
            : File.ReadAllText(value);
    }

    public static HostConfig FromEnvironment() {
        var connString = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION")
            ?? throw new InvalidOperationException("IDENTITY_DB_CONNECTION is required");
        var apiSecret = Environment.GetEnvironmentVariable("IDENTITY_API_SECRET")
            ?? throw new InvalidOperationException("IDENTITY_API_SECRET is required");

        var authentikAuthority = Environment.GetEnvironmentVariable("AUTHENTIK_AUTHORITY");
        var authentikTokenDecryptionKey = ReadPemOrFile(
            "AUTHENTIK_TOKEN_DECRYPTION_KEY", Environment.GetEnvironmentVariable("AUTHENTIK_TOKEN_DECRYPTION_KEY"));
        var loginWidgetEnabled = !string.IsNullOrEmpty(authentikAuthority);

        var sessionOptions = SessionCookieOptions.FromEnvironment();
        var avatarStorageDir = Environment.GetEnvironmentVariable("AVATAR_STORAGE_DIR");

        return new HostConfig {
            ConnString = connString,
            ApiSecret = apiSecret,
            Port = Environment.GetEnvironmentVariable("IDENTITY_API_PORT") ?? "8090",
            AdminIds = Environment.GetEnvironmentVariable("IDENTITY_ADMIN_DISCORD_IDS"),
            LocalLoginKey = Environment.GetEnvironmentVariable("EGGIDENTITY_LOCAL_KEY"),
            AuthentikAuthority = authentikAuthority,
            AuthentikTokenDecryptionKey = authentikTokenDecryptionKey,
            AuthentikApiToken = Environment.GetEnvironmentVariable("AUTHENTIK_API_TOKEN"),
            LoginWidgetEnabled = loginWidgetEnabled,
            SessionOptions = sessionOptions,
            AvatarStorageDir = avatarStorageDir,
            ProfileEnabled = loginWidgetEnabled && sessionOptions is not null && !string.IsNullOrEmpty(avatarStorageDir),
        };
    }
}
