using Discord;
using EggIdentity.Contract;
using EggIdentity.Db;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Bot;

public sealed class EggIdentityBotBuilder {
    private string _configFilePath = "/etc/eggidentity/config.env";
    private Func<string, string?> _envFallback = Environment.GetEnvironmentVariable;
    private string _name = "";
    private VerifyInfo _build = new();
    private bool _globalCommands;
    private bool _guildCommandMirror;
    private readonly List<BotCommand> _commands = [];
    private EmbedOptions? _verifyOptions;
    private Func<BotConfig, Embed>? _verifyBuilder;
    private string? _dbConnStr;
    private string? _dbMigrationsDir;
    private string _migrationsDir = "Migrations";
    private string _migrationsTableName = "eggidentity_migrations";
    private Func<NewVersionEvent, Task>? _newVersionHandler;
    private string _eventSecret = "";
    private Func<CancellationToken, Task<DashboardSnapshot>>? _dashboardProvider;
    private Func<CancellationToken, Task<IReadOnlyList<BotAppSnapshot>>>? _servedApps;
    private TimeSpan _dashboardRefreshInterval = TimeSpan.FromMinutes(5);

    public EggIdentityBotBuilder WithConfigFile(string path) { _configFilePath = path; return this; }
    public EggIdentityBotBuilder WithEnvFallback(Func<string, string?> envFallback) { _envFallback = envFallback; return this; }
    public EggIdentityBotBuilder WithName(string name) { _name = name; return this; }
    public EggIdentityBotBuilder WithBuild(VerifyInfo build) { _build = build; return this; }
    public EggIdentityBotBuilder WithGlobalCommands(bool enabled = true) { _globalCommands = enabled; return this; }
    public EggIdentityBotBuilder WithGuildCommandMirror(bool enabled = true) { _guildCommandMirror = enabled; return this; }
    public EggIdentityBotBuilder WithCommand(BotCommand command) { _commands.Add(command); return this; }
    public EggIdentityBotBuilder WithVerifyEmbed(EmbedOptions options) { _verifyOptions = options; return this; }
    public EggIdentityBotBuilder WithVerifyEmbedBuilder(Func<BotConfig, Embed> build) { _verifyBuilder = build; return this; }
    public EggIdentityBotBuilder WithDb(string connStr, string migrationsDir) { _dbConnStr = connStr; _dbMigrationsDir = migrationsDir; return this; }
    public EggIdentityBotBuilder WithMigrationsLocation(string dir, string tableName) { _migrationsDir = dir; _migrationsTableName = tableName; return this; }
    public EggIdentityBotBuilder WithNewVersionHandler(Func<NewVersionEvent, Task> handler, string eventSecret) { _newVersionHandler = handler; _eventSecret = eventSecret; return this; }
    public EggIdentityBotBuilder WithDashboardProvider(Func<CancellationToken, Task<DashboardSnapshot>> provider) { _dashboardProvider = provider; return this; }
    public EggIdentityBotBuilder WithServedApps(Func<CancellationToken, Task<IReadOnlyList<BotAppSnapshot>>> served) { _servedApps = served; return this; }
    public EggIdentityBotBuilder WithDashboardRefreshInterval(TimeSpan interval) { _dashboardRefreshInterval = interval; return this; }

    public BotConfig BuildConfig() {
        var values = BotConfigLoader.Load(_configFilePath, _envFallback);
        return new BotConfig {
            Name = _name,
            Token = values.Token ?? "",
            AppId = values.AppId ?? "",
            GuildId = values.GuildId ?? "",
            RepoUrl = values.RepoUrl ?? "",
            SharedRoleId = values.SharedRoleId ?? "",
            SupporterRoleId = values.SupporterRoleId ?? "",
            PostgresConnectionString = values.PostgresConnectionString ?? "",
            DashboardChannelId = values.DashboardChannelId ?? "",
            MigrationsDir = _migrationsDir,
            MigrationsTableName = _migrationsTableName,
            DashboardProvider = _dashboardProvider,
            ServedApps = _servedApps,
            DashboardRefreshInterval = _dashboardRefreshInterval,
            Build = _build,
            GlobalCommands = _globalCommands,
            GuildCommandMirror = _guildCommandMirror,
            Extra = _commands,
            VerifyEmbedOptions = _verifyOptions,
            VerifyEmbedBuilder = _verifyBuilder,
        };
    }

    public Embed ResolveVerifyEmbed(BotConfig cfg) =>
        cfg.VerifyEmbedBuilder is not null ? cfg.VerifyEmbedBuilder(cfg)
        : cfg.VerifyEmbedOptions is not null ? cfg.VerifyEmbedOptions.Apply(DefaultEmbeds.Verify(cfg))
        : DefaultEmbeds.Verify(cfg);

    public async Task RunAsync(Action<WebApplication>? configureRoutes = null) {
        var cfg = BuildConfig();
        var webBuilder = WebApplication.CreateBuilder();
        var app = webBuilder.Build();

        Npgsql.NpgsqlConnection? conn = null;
        if (!string.IsNullOrEmpty(_dbConnStr)) {
            conn = await Database.InitAsync(_dbConnStr);
            if (!string.IsNullOrEmpty(_dbMigrationsDir))
                await Migrator.MigrateAsync(conn, _dbMigrationsDir);
        }

        EggIdentityBot? bot = null;
        try {
            bot = await EggIdentityBot.StartAsync(cfg, this);
        } catch (Discord.WebSocket.GatewayReconnectException ex) {
            app.Logger.LogWarning(ex,
                "eggidentity: bot start failed - gateway rejected the connection, likely because the " +
                "GuildMembers privileged intent isn't enabled for this bot application. Enable " +
                "\"Server Members Intent\" in the Discord Developer Portal (Bot tab), continuing without bot");
        } catch (Exception ex) {
            app.Logger.LogWarning(ex, "eggidentity: bot start failed, continuing");
        }

        if (_newVersionHandler is not null)
            app.MapPost("/events/new-version", NewVersionHandler.Build(_eventSecret, _newVersionHandler));

        var values = BotConfigLoader.Load(_configFilePath, _envFallback);

        Npgsql.NpgsqlDataSource? botDataSource = null;
        ChannelConfigStore? channelConfigStore = null;
        if (bot is not null && !string.IsNullOrEmpty(cfg.PostgresConnectionString)) {
            botDataSource = Npgsql.NpgsqlDataSource.Create(cfg.PostgresConnectionString);
            await using (var botConn = await botDataSource.OpenConnectionAsync())
                await Migrator.MigrateAsync(botConn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
            channelConfigStore = new ChannelConfigStore(botDataSource);
        }

        if (bot is not null && channelConfigStore is not null && botDataSource is not null &&
            ulong.TryParse(cfg.GuildId, out var notifyGuildId)) {
            var notifier = new DeployNotifier(channelConfigStore, bot.Client, notifyGuildId, cfg.Name);
            var deployStateStore = new DeployStateStore(botDataSource);
            var tracker = new DeployVersionTracker(deployStateStore, notifier);
            try {
                await tracker.CheckAndNotifyAsync(
                    cfg.Name, Environment.GetEnvironmentVariable("GIT_SHA") ?? "", cfg.Build.Version, CancellationToken.None);
            } catch (Exception ex) {
                app.Logger.LogWarning(ex, "eggidentity: deploy self-report failed, continuing");
            }
        }

        configureRoutes?.Invoke(app);

        var addr = Environment.GetEnvironmentVariable("LISTEN_ADDR");
        if (string.IsNullOrEmpty(addr)) addr = ":8080";
        var urls = addr.StartsWith(':') ? $"http://0.0.0.0{addr}" : $"http://{addr}";

        app.Logger.LogInformation("eggidentity: {Name} listening on {Addr}", cfg.Name, addr);
        try { await app.RunAsync(urls); } finally {
            if (bot is not null) await bot.DisposeAsync();
            if (conn is not null) await conn.DisposeAsync();
        }
    }
}
