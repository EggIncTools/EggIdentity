using Discord;
using EggIdentity.Contract;

namespace EggIdentity.Bot;

public sealed class BotConfig {
    public string Name { get; init; } = "";
    public string Token { get; init; } = "";
    public string AppId { get; init; } = "";
    public string GuildId { get; init; } = "";
    public string RepoUrl { get; init; } = "";
    public VerifyInfo Build { get; init; } = new();
    public string SharedRoleId { get; init; } = "";
    public string SupporterRoleId { get; init; } = "";
    public IReadOnlyList<BotCommand> Extra { get; init; } = Array.Empty<BotCommand>();

    public EmbedOptions? VerifyEmbedOptions { get; init; }
    public Func<BotConfig, Embed>? VerifyEmbedBuilder { get; init; }

    public bool GlobalCommands { get; init; }
    public bool GuildCommandMirror { get; init; }

    public string DashboardChannelId { get; init; } = "";
    public string PostgresConnectionString { get; init; } = "";
    public string MigrationsDir { get; init; } = "Migrations";
    public string MigrationsTableName { get; init; } = "eggidentity_migrations";

    public Func<CancellationToken, Task<DashboardSnapshot>>? DashboardProvider { get; init; }
    public Func<CancellationToken, Task<IReadOnlyList<BotAppSnapshot>>>? ServedApps { get; init; }
    public TimeSpan DashboardRefreshInterval { get; init; } = TimeSpan.FromMinutes(5);

    public string CommitUrl(string version) => $"{RepoUrl}/commit/{version}";

    public BotConfig WithRegistration(DiscordRegistrationResponse registration) {
        ArgumentNullException.ThrowIfNull(registration);
        return new BotConfig {
            Name = Name,
            Token = registration.Token,
            AppId = registration.AppId ?? AppId,
            GuildId = registration.GuildId,
            RepoUrl = RepoUrl,
            Build = Build,
            SharedRoleId = SharedRoleId,
            SupporterRoleId = SupporterRoleId,
            Extra = Extra,
            VerifyEmbedOptions = VerifyEmbedOptions,
            VerifyEmbedBuilder = VerifyEmbedBuilder,
            GlobalCommands = GlobalCommands,
            GuildCommandMirror = GuildCommandMirror,
            DashboardChannelId = registration.DashboardChannelId ?? DashboardChannelId,
            PostgresConnectionString = PostgresConnectionString,
            MigrationsDir = MigrationsDir,
            MigrationsTableName = MigrationsTableName,
            DashboardProvider = DashboardProvider,
            ServedApps = ServedApps,
            DashboardRefreshInterval = DashboardRefreshInterval,
        };
    }
}

public sealed record BotCommand(
    ApplicationCommandProperties Definition,
    string Name,
    Func<SocketSlashCommandContext, Task> Handler,
    Func<SocketAutocompleteContext, Task>? AutocompleteHandler = null);

public sealed record SocketSlashCommandContext(
    Discord.WebSocket.DiscordSocketClient Client,
    Discord.WebSocket.SocketSlashCommand Command);

public sealed record SocketAutocompleteContext(
    Discord.WebSocket.DiscordSocketClient Client,
    Discord.WebSocket.SocketAutocompleteInteraction Interaction);
