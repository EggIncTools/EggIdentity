using System.Reflection;
using Discord.WebSocket;
using EggIdentity.Bot;
using EggIdentity.Contract;
using EggIdentity.Db;
using EggIdentity.Fleet;
using Npgsql;

namespace EggIdentity.Host;

public sealed class BotHostedService(
    string configFilePath, string postgresConnectionString, SuiteBotApps suiteApps, DeployService fleet, TimeProvider? time = null) : IHostedService {
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public EggIdentityBot? Bot { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken) {
        var build = BuildInfo.Build(Environment.GetEnvironmentVariable, Assembly.GetExecutingAssembly());
        var startedAt = _time.GetUtcNow();

        var builder = new EggIdentityBotBuilder()
            .WithConfigFile(configFilePath)
            .WithEnvFallback(key => key == "POSTGRES_CONNECTION_STRING" ? postgresConnectionString : Environment.GetEnvironmentVariable(key))
            .WithName("EggIdentity")
            .WithBuild(build)
            .WithMigrationsLocation("BotMigrations", "eggidentity_bot_migrations")
            .WithDashboardProvider(_ => Task.FromResult(new DashboardSnapshot {
                AppName = "EggIdentity",
                Version = build.Version,
                BuildHash = build.Sha256,
                UptimeSince = startedAt,
                RepoUrl = "https://github.com/EggIncTools/eggidentity",
            }))
            .WithServedApps(ServedSnapshotsAsync);

        var cfg = builder.BuildConfig();

        try {
            Bot = await EggIdentityBot.StartAsync(cfg, builder);
        } catch (GatewayReconnectException ex) {
            Console.Error.WriteLine(
                $"eggidentity: bot start failed - gateway rejected the connection, likely because the " +
                $"GuildMembers privileged intent isn't enabled for this bot application: {ex.Message}");
        } catch (Exception ex) {
            Console.Error.WriteLine($"eggidentity: bot start failed, continuing: {ex.Message}");
        }

        if (Bot is null || !ulong.TryParse(cfg.GuildId, out var guildId))
            return;

        fleet.Events.Published += OnFleetEvent;

        var dataSource = NpgsqlDataSource.Create(postgresConnectionString);
        await using (var conn = await dataSource.OpenConnectionAsync(cancellationToken))
            await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "BotMigrations"), "eggidentity_bot_migrations", cancellationToken);

        var channelConfigStore = new ChannelConfigStore(dataSource);
        var notifier = new DeployNotifier(channelConfigStore, Bot.Client, guildId, cfg.Name);
        var deployStateStore = new DeployStateStore(dataSource);
        var tracker = new DeployVersionTracker(deployStateStore, notifier);
        try {
            await tracker.CheckAndNotifyAsync(cfg.Name, Environment.GetEnvironmentVariable("GIT_SHA") ?? "", cfg.Build.Version, cancellationToken);
        } catch (Exception ex) {
            Console.Error.WriteLine($"eggidentity: deploy self-report failed, continuing: {ex.Message}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        fleet.Events.Published -= OnFleetEvent;
        if (Bot is not null) await Bot.DisposeAsync();
    }

    private async Task<IReadOnlyList<BotAppSnapshot>> ServedSnapshotsAsync(CancellationToken ct) {
        var snapshots = await suiteApps.SnapshotsAsync(ct);
        if (Bot is not { } bot) return snapshots;
        foreach (var app in snapshots.Where(s => s.Snapshot.BuildHash.Length > 0)) {
            try {
                await bot.TrackDeployAsync(app.App, app.Snapshot.BuildHash, app.Snapshot.Version, ct);
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                Console.Error.WriteLine($"eggidentity: deploy tracking for {app.App} failed: {ex.Message}");
            }
        }
        return snapshots;
    }

    private void OnFleetEvent(DeployEvent evt) {
        if (evt.Phase is not (DeployPhase.Deployed or DeployPhase.Failed)) return;
        _ = Task.Run(() => ReportAsync(evt), CancellationToken.None);
    }

    private async Task ReportAsync(DeployEvent evt) {
        if (Bot is not { } bot) return;
        try {
            var served = await suiteApps.ServedAsync(CancellationToken.None);
            if (!served.Any(a => string.Equals(a.Name, evt.App, StringComparison.OrdinalIgnoreCase))) return;
            if (evt.Phase == DeployPhase.Deployed && !string.IsNullOrEmpty(evt.ToRevision))
                await bot.TrackDeployAsync(evt.App, evt.ToRevision, evt.Version ?? "", CancellationToken.None);
            else if (evt.Phase == DeployPhase.Failed)
                await bot.NotifyDeployAsync(evt.App, new DeployResponse { Ok = false, Tail = evt.Message, FromHash = evt.FromRevision }, CancellationToken.None);
        } catch (Exception ex) {
            Console.Error.WriteLine($"eggidentity: deploy notice for {evt.App} failed: {ex.Message}");
        }
    }
}
