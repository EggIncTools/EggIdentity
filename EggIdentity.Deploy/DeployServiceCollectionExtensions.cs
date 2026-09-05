using EggIdentity.Auth;
using EggIdentity.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Deploy;

public static class DeployServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityDeploy(this IServiceCollection services, DeployOptions options, SessionCookieOptions session) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(session);

        services.AddSingleton(options);
        services.AddHttpClient(DeployOptions.HttpClientName, http => {
            http.BaseAddress = options.BaseAddress;
            http.Timeout = Timeout.InfiniteTimeSpan;
        })
            .AddTypedClient((http, _) => new AgentClient(http, options, session));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DeployEventHub>();
        services.AddSingleton<IDeployEvents>(sp => sp.GetRequiredService<DeployEventHub>());
        services.AddHostedService(sp => new DeployEventListener(
            sp.GetRequiredService<AgentClient>(),
            sp.GetRequiredService<DeployEventHub>(),
            options,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetService<ILogger<DeployEventListener>>()));

        services.AddTransient<IEnvSource, AgentEnvSource>();
        services.AddTransient<IRestartTrigger, AgentRestartTrigger>();
        services.AddTransient<IStackEnvEditor, AgentStackEnvEditor>();
        return services;
    }

    public static IServiceCollection AddEggIdentityDeployFromEnvironment(this IServiceCollection services, string appName) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);

        var agentUrl = Environment.GetEnvironmentVariable(DeployOptions.AgentUrlEnv);
        var session = SessionCookieOptions.FromEnvironment();
        if (string.IsNullOrWhiteSpace(agentUrl) || session is null) return services;

        return services.AddEggIdentityDeploy(new DeployOptions(agentUrl, appName) { CallerName = appName }, session);
    }
}
