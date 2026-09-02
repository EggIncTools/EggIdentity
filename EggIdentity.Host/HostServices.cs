using System.Net.Http.Headers;
using EggIdentity.Auth;
using EggIdentity.Client;
using EggIdentity.Fallback;
using EggIdentity.Settings;
using EggIdentity.Settings.AdminUi;
using EggIdentity.Settings.Store;
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
        builder.Services.AddSingleton(AdminAllowlist.FromConfig(config.AdminIds));
        builder.Services.AddSingleton<IdentityResolver>();
        builder.Services.AddSingleton<RevocationStore>();
        builder.Services.AddSingleton<UserQueries>();
        builder.Services.AddSingleton<ProfileService>();
        builder.Services.AddSingleton<LoginCodeStore>();
        builder.Services.AddSingleton<OAuthStateStore>();
        builder.Services.AddHttpClient();
        builder.Services.AddEggIdentityFallback(new FallbackBranding("EggIdentity", FallbackDefaults.Tokens));

        RegisterSponsors(builder, config);
        RegisterLoginWidget(builder, config);
        RegisterBot(builder, config);
        var runtime = RegisterSettings(builder, config, dataSource);
        RegisterAdmin(builder, config);

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
        if (!config.LoginWidgetEnabled) return;

        var authority = config.AuthentikAuthority!;
        builder.Services.AddSingleton(sp => new IconCache(sp.GetRequiredService<IHttpClientFactory>(), authority));
        builder.Services.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever()));
    }

    private static void RegisterBot(WebApplicationBuilder builder, HostConfig config) {
        if (!config.BotEnabled) return;

        builder.Services.AddSingleton(new BotHostedService(config.BotConfigFilePath, config.ConnString));
        builder.Services.AddHostedService(sp => sp.GetRequiredService<BotHostedService>());
        builder.Services.AddScoped(sp => sp.GetRequiredService<BotHostedService>().Bot?.ConfigService!);
    }

    private static HostRuntime RegisterSettings(
        WebApplicationBuilder builder, HostConfig config, NpgsqlDataSource dataSource) {
        var registry = new SettingsRegistry([HostSettings.Provider, SessionSettings.Provider]);
        var store = new SettingsStore(dataSource, SecretProtector.FromEnvironment());
        var cache = new SettingsCache(registry, store, config.SharedFileLookup);

        builder.Services.AddSingleton(registry);
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton(cache);
        builder.Services.AddSingleton(new SettingsAdminService(registry, store, cache));

        return new HostRuntime(dataSource, store, cache);
    }

    private static void RegisterAdmin(WebApplicationBuilder builder, HostConfig config) {
        if (!config.AdminEnabled) return;

        var agentUrl = Environment.GetEnvironmentVariable("DEPLOY_AGENT_URL");
        if (!string.IsNullOrEmpty(agentUrl)) {
            builder.Services.AddSingleton(sp => new AgentStackClient(
                sp.GetRequiredService<IHttpClientFactory>(), config.SessionOptions!, agentUrl, "eggidentity"));
            builder.Services.AddSingleton<IStackEnvSource>(sp => sp.GetRequiredService<AgentStackClient>());
            builder.Services.AddSingleton<IRestartTrigger>(sp => sp.GetRequiredService<AgentStackClient>());
        }

        builder.Services.AddHttpClient<IdentityApiClient>(c => {
            c.BaseAddress = new Uri($"http://localhost:{config.Port}");
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiSecret);
        });
        builder.Services.AddAuthentication(EggIdentitySessionDefaults.Scheme)
            .AddEggIdentitySession(config.SessionOptions!);
        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
    }
}
