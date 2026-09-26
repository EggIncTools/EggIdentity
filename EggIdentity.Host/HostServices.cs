using EggIdentity.Auth;
using EggIdentity.Deploy;
using EggIdentity.Fallback;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Npgsql;

namespace EggIdentity.Host;

internal sealed record HostRuntime(
    NpgsqlDataSource DataSource,
    SettingsStore SettingsStore,
    SettingsCache SettingsCache);

internal static class HostServices {
    public static HostRuntime Register(WebApplicationBuilder builder, HostConfig config) {
        builder.WebHost.UseUrls($"http://*:{config.Port}");

        var dataSource = NpgsqlDataSource.Create(config.ConnString);
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
        var runtime = RegisterSettings(builder, config, dataSource);
        RegisterDeploy(builder, config);

        return runtime;
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
            sp.GetRequiredService<SettingsCache>(), authority, config.AuthentikAppsDir, config.AuthentikTokenDecryptionKey));
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
        if (!config.BotEnabled) return;

        builder.Services.AddSingleton(new BotHostedService(config.BotConfigFilePath, config.ConnString));
        builder.Services.AddHostedService(sp => sp.GetRequiredService<BotHostedService>());
    }

    private static HostRuntime RegisterSettings(
        WebApplicationBuilder builder, HostConfig config, NpgsqlDataSource dataSource) {
        var registry = SettingsRegistry.Compose(
            [HostSettings.Provider, SessionSettings.Provider], [DeployApps.Provider, DeployStacks.Provider, AuthentikApps.Provider]);
        var store = new SettingsStore(dataSource, SecretProtector.FromEnvironment());
        var cache = new SettingsCache(registry, store, config.SharedFileLookup);

        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(cache);
        builder.Services.AddSingleton(new SettingsAdminService(registry, store, cache));

        return new HostRuntime(dataSource, store, cache);
    }

    private static void RegisterDeploy(WebApplicationBuilder builder, HostConfig config) {
        if (string.IsNullOrWhiteSpace(config.DeployAgentUrl) || config.SessionOptions is null) return;
        builder.Services.AddEggIdentityDeploy(
            new DeployOptions(config.DeployAgentUrl, "eggidentity") { CallerName = "eggidentity-host" },
            config.SessionOptions);
    }
}
