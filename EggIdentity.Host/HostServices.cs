using EggIdentity.Auth;
using EggIdentity.Db;
using EggIdentity.DbClone;
using EggIdentity.Deploy;
using EggIdentity.Fallback;
using EggIdentity.Fleet;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Npgsql;

namespace EggIdentity.Host;

internal sealed record HostRuntime(
    NpgsqlDataSource DataSource,
    SettingsRegistry Registry,
    SettingsStore SettingsStore,
    SettingsCache SettingsCache);

internal static class HostServices {
    public const string AppName = "eggidentity";

    public static async Task<HostRuntime> CreateRuntimeAsync(HostConfig config) {
        var dataSource = NpgsqlDataSource.Create(config.ConnString);
        await using (var conn = await Database.WaitForAsync(dataSource))
            await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));

        var registry = SettingsRegistry.Compose(
            [HostSettings.Provider, SessionSettings.Provider, EnvironmentSettings.Provider, FleetSettings.Provider],
            [SuiteApps.Provider, SuiteUpstreams.Provider]);
        var store = new SettingsStore(dataSource, SecretProtector.FromEnvironment());
        await store.MigrateAsync();
        var cache = new SettingsCache(registry, store);
        await cache.GetAsync();
        return new HostRuntime(dataSource, registry, store, cache);
    }

    public static void Register(WebApplicationBuilder builder, HostConfig config, HostRuntime runtime) {
        builder.WebHost.UseUrls($"http://*:{config.Port}");

        var dataSource = runtime.DataSource;
        builder.Services.AddSingleton(dataSource);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(AdminAllowlist.FromConfig(config.AdminIds));
        builder.Services.AddSingleton<IdentityResolver>();
        builder.Services.AddSingleton<RevocationStore>();
        builder.Services.AddSingleton<UserQueries>();
        builder.Services.AddSingleton<ProfileService>();
        builder.Services.AddSingleton<ConsentService>();
        builder.Services.AddSingleton<LoginCodeStore>();
        builder.Services.AddSingleton<OAuthStateStore>();
        builder.Services.AddHttpClient();
        builder.Services.AddEggIdentityFallback(new FallbackBranding("EggIdentity", FallbackDefaults.Tokens));

        RegisterSponsors(builder, config);
        RegisterLoginWidget(builder, config);
        RegisterBot(builder, config);
        RegisterSettings(builder, runtime);
        RegisterFleet(builder);
    }

    private static void RegisterSponsors(WebApplicationBuilder builder, HostConfig config) {
        if (config.SponsorConfig is not { } sponsorConfig) return;

        builder.Services.AddSingleton(sponsorConfig);
        builder.Services.AddSingleton<GitHubSponsorStatusStore>();
        builder.Services.AddSingleton<IGitHubSponsorClient>(sp =>
            new GitHubSponsorClient(sp.GetRequiredService<IHttpClientFactory>(), sponsorConfig.GitHubPat, sponsorConfig.GitHubTarget));
        builder.Services.AddSingleton<IDiscordRoleClient>(sp =>
            new DiscordRoleClient(sp.GetRequiredService<IHttpClientFactory>(), sponsorConfig.DiscordBotToken));
        builder.Services.AddSingleton<SponsorSyncService>();
        builder.Services.AddSingleton<SupporterStatusService>();
    }

    private static void RegisterLoginWidget(WebApplicationBuilder builder, HostConfig config) {
        if (!config.LoginWidgetEnabled || config.AuthentikAuthority is not { } authority) return;

        builder.Services.AddSingleton(sp => new IconCache(sp.GetRequiredService<IHttpClientFactory>(), authority));
        builder.Services.AddSingleton(sp => new AppAuthConfigs(
            sp.GetRequiredService<SettingsCache>(), authority, config.AuthentikTokenDecryptionKey));
        builder.Services.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever()));

        if (string.IsNullOrWhiteSpace(config.AuthentikApiToken)) return;
        builder.Services.AddSingleton<IAuthentikAdminClient>(sp =>
            new AuthentikAdminClient(sp.GetRequiredService<IHttpClientFactory>(), authority, config.AuthentikApiToken));
        builder.Services.AddSingleton<IdentityReconciler>();
        builder.Services.AddSingleton(sp => new IdentityReconcileService(
            sp.GetRequiredService<IdentityReconciler>(), sp.GetRequiredService<UserQueries>(),
            TimeSpan.FromMinutes(config.ReconcileIntervalMinutes)));
    }

    private static void RegisterBot(WebApplicationBuilder builder, HostConfig config) {
        if (config.DiscordRegistration is not { } registration) return;

        builder.Services.AddSingleton<SuiteBotApps>();
        builder.Services.AddSingleton(sp => new BotHostedService(
            registration, config.RepoUrl, config.ConnString, sp.GetRequiredService<SuiteBotApps>(), sp.GetRequiredService<DeployService>()));
        builder.Services.AddHostedService(sp => sp.GetRequiredService<BotHostedService>());
    }

    private static void RegisterSettings(WebApplicationBuilder builder, HostRuntime runtime) {
        builder.Services.AddSingleton(runtime.Registry);
        builder.Services.AddSingleton(runtime.SettingsStore);
        builder.Services.AddSingleton(runtime.SettingsCache);
        builder.Services.AddSingleton(new SettingsAdminService(runtime.Registry, runtime.SettingsStore, runtime.SettingsCache));
    }

    private static void RegisterFleet(WebApplicationBuilder builder) {
        builder.Services.AddSingleton(sp => FleetRuntime.Create(
            sp.GetRequiredService<SettingsCache>(), logger: sp.GetRequiredService<ILoggerFactory>().CreateLogger("EggIdentity.Fleet"),
            builds: new UpstreamBuildStore(sp.GetRequiredService<NpgsqlDataSource>()),
            pinStates: new PinStateStore(sp.GetRequiredService<NpgsqlDataSource>())));
        builder.Services.AddSingleton(sp => sp.GetRequiredService<FleetRuntime>().Service);
        builder.Services.AddTransient<IEnvSource>(sp => new FleetEnvSource(sp.GetRequiredService<DeployService>(), AppName));
        builder.Services.AddTransient<IRestartTrigger>(sp => new FleetRestartTrigger(sp.GetRequiredService<DeployService>(), AppName));
        builder.Services.AddTransient<IStackEnvEditor>(sp => new FleetStackEnvEditor(sp.GetRequiredService<DeployService>(), AppName));
    }
}
