using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed record SuiteApp {
    public const string ProdEnvironment = "prod";
    public const string SubProdEnvironment = "subprod";
    public const string DiscordBotNone = "none";
    public const string DiscordBotOwn = "own";
    public const string DiscordBotSuite = "suite";

    public string Name { get; init; } = "";
    public string? DisplayName { get; init; }
    public string Environment { get; init; } = ProdEnvironment;
    public bool Enabled { get; init; } = true;
    public string? PublicUrl { get; init; }
    public string? RepoUrl { get; init; }
    public string? BrandSlug { get; init; }
    public bool Listed { get; init; }
    public string? Stack { get; init; }
    public string? Container { get; init; }
    public bool AutoDeploy { get; init; } = true;
    public string? AdminBaseUrl { get; init; }
    public string? AuthClientId { get; init; }
    public string? AuthClientSecret { get; init; }
    public string? AuthCallbackUrl { get; init; }
    public string? AuthEndSessionUrl { get; init; }
    public string DiscordBot { get; init; } = DiscordBotNone;
    public string? DiscordToken { get; init; }
    public string? DiscordAppId { get; init; }
    public string? DiscordGuildId { get; init; }
    public string? DiscordDashboardChannelId { get; init; }

    public string ContainerName => string.IsNullOrWhiteSpace(Container) ? Name : Container.Trim();

    public string Label => string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName.Trim();

    public bool IsSubProd => string.Equals(Environment, SubProdEnvironment, StringComparison.OrdinalIgnoreCase);

    public string? AuthOrigin =>
        Uri.TryCreate(PublicUrl?.Trim(), UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? $"{url.Scheme}://{url.Authority}"
            : null;

    public bool HasLogin => !string.IsNullOrWhiteSpace(AuthClientId) && AuthOrigin is not null;

    public bool ServedBySuiteBot => string.Equals(DiscordBot, DiscordBotSuite, StringComparison.OrdinalIgnoreCase);

    public bool RunsOwnBot => string.Equals(DiscordBot, DiscordBotOwn, StringComparison.OrdinalIgnoreCase);
}

public static class SuiteApps {
    public const string Key = "suite.apps";
    public const string IdentityGroup = "Identity";
    public const string RuntimeGroup = "Runtime";
    public const string AdminGroup = "Admin";
    public const string LoginGroup = "Login";
    public const string DiscordGroup = "Discord";

    private static readonly FieldCondition OwnBot = new("discord_bot", SuiteApp.DiscordBotOwn);

    public static CollectionDescriptor Descriptor { get; } = new(
        Key, "Suite apps", "Suite",
        [
            new FieldDescriptor("name", "App name", SettingKind.Text) {
                Required = true,
                Group = IdentityGroup,
                Description = "Unique per instance. A sub-prod instance is its own row with its own name.",
            },
            new FieldDescriptor("display_name", "Display name", SettingKind.Text) { Group = IdentityGroup },
            new FieldDescriptor("environment", "Environment", SettingKind.Enum) {
                Default = SuiteApp.ProdEnvironment,
                EnumValues = [SuiteApp.ProdEnvironment, SuiteApp.SubProdEnvironment],
                Group = IdentityGroup,
            },
            new FieldDescriptor("enabled", "Enabled", SettingKind.Bool) {
                Default = "true",
                Group = IdentityGroup,
                Description = "Off hides the app from the fleet, the hub and login without deleting it.",
            },
            new FieldDescriptor("public_url", "Public URL", SettingKind.Url) {
                Group = IdentityGroup,
                Description = "Its origin is also the login origin and the returnUrl allowlist entry.",
            },
            new FieldDescriptor("repo_url", "Repository URL", SettingKind.Url) {
                Group = IdentityGroup,
                Description = "Builds commit links from image revision labels.",
            },
            new FieldDescriptor("brand_slug", "Brand slug", SettingKind.Text) {
                Group = IdentityGroup,
                Description = "Joins to the brand marks in EggIdentity.Contract.",
            },
            new FieldDescriptor("listed", "Listed publicly", SettingKind.Bool) {
                Default = "false",
                Group = IdentityGroup,
                Description = "Shows this app on suite landing pages.",
            },
            new FieldDescriptor("stack", "Portainer stack", SettingKind.Text) {
                Group = RuntimeGroup,
                Description = "Name of the Portainer stack that runs this app. Its id and endpoint are read from Portainer.",
            },
            new FieldDescriptor("container", "Container", SettingKind.Text) {
                Group = RuntimeGroup,
                Description = "Defaults to the app name.",
            },
            new FieldDescriptor("auto_deploy", "Auto deploy", SettingKind.Bool) {
                Default = "true",
                Group = RuntimeGroup,
                Description = "Deploy as soon as CI reports a new image.",
            },
            new FieldDescriptor("admin_base_url", "Admin base URL", SettingKind.Url) {
                Group = AdminGroup,
                Description = "Internal address serving /admin/api, for example http://eggledger:5015. Its secret is read from ADMIN_SECRET_<NAME> on the hub.",
            },
            new FieldDescriptor("auth_client_id", "Client id", SettingKind.Text) {
                Group = LoginGroup,
                Description = "Authentik OAuth client id. Set to let this app log in through the identity host.",
            },
            new FieldDescriptor("auth_client_secret", "Client secret", SettingKind.Secret, Sensitivity.Secret) { Group = LoginGroup },
            new FieldDescriptor("auth_callback_url", "Callback URL", SettingKind.Url) {
                Group = LoginGroup,
                Description = "Authentik provider redirect URI. Points at the identity host's /auth/callback.",
            },
            new FieldDescriptor("auth_end_session_url", "End-session URL", SettingKind.Url) {
                Group = LoginGroup,
                Description = "Falls back to OIDC discovery when empty.",
            },
            new FieldDescriptor("discord_bot", "Discord bot", SettingKind.Enum) {
                Default = SuiteApp.DiscordBotNone,
                EnumValues = [SuiteApp.DiscordBotNone, SuiteApp.DiscordBotOwn, SuiteApp.DiscordBotSuite],
                Group = DiscordGroup,
                Description = "own: the app runs its own bot from the registration below. suite: the identity host's bot posts its dashboard, GitHub feed and deploy notices.",
            },
            new FieldDescriptor("discord_token", "Bot token", SettingKind.Secret, Sensitivity.Secret) {
                Group = DiscordGroup,
                VisibleWhen = OwnBot,
                Description = "The app's own Discord application bot token. The app fetches it from the identity host at startup.",
            },
            new FieldDescriptor("discord_app_id", "Application id", SettingKind.Snowflake) { Group = DiscordGroup, VisibleWhen = OwnBot },
            new FieldDescriptor("discord_guild_id", "Guild id", SettingKind.Snowflake) { Group = DiscordGroup, VisibleWhen = OwnBot },
            new FieldDescriptor("discord_dashboard_channel_id", "Dashboard channel id", SettingKind.Snowflake) {
                Group = DiscordGroup,
                VisibleWhen = OwnBot,
                Description = "Fallback dashboard channel until one is set in the bot config.",
            },
        ],
        "name", "display_name") {
        Description = "One row per deployed app instance: what it is, where it runs, how it is administered, how it logs in and who speaks for it on Discord.",
    };

    public static ICollectionProvider Provider { get; } = new StaticCollectionProvider([Descriptor]);

    public static IReadOnlyList<string> Problems(SuiteApp app) {
        ArgumentNullException.ThrowIfNull(app);
        var problems = new List<string>();
        var loginFields = new[] { app.AuthClientId, app.AuthClientSecret, app.AuthCallbackUrl };
        var loginSet = loginFields.Count(v => !string.IsNullOrWhiteSpace(v));
        if (loginSet is > 0 and < 3) problems.Add("Login needs a client id, a client secret and a callback URL together.");
        if (loginSet > 0 && app.AuthOrigin is null) problems.Add("Login needs an absolute public URL; its origin is the login origin.");
        if (app.Enabled && string.IsNullOrWhiteSpace(app.Stack)) problems.Add("No Portainer stack, so the fleet cannot deploy or restart it.");
        if (app.RunsOwnBot && (string.IsNullOrWhiteSpace(app.DiscordToken) || string.IsNullOrWhiteSpace(app.DiscordGuildId)))
            problems.Add("Its own Discord bot needs a bot token and a guild id.");
        return problems;
    }
}
