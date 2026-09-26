using EggIdentity.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Deploy;

public static class DeployServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityDeploy(this IServiceCollection services, DeployOptions options) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);
        services.AddHttpClient(DeployOptions.HttpClientName, http => http.Timeout = Timeout.InfiniteTimeSpan);
        services.AddSingleton(sp => new FleetClient(sp.GetRequiredService<IHttpClientFactory>(), options));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<DeployEventHub>();
        services.AddSingleton<IDeployEvents>(sp => sp.GetRequiredService<DeployEventHub>());
        services.AddHostedService(sp => new DeployEventListener(
            sp.GetRequiredService<FleetClient>(),
            sp.GetRequiredService<DeployEventHub>(),
            options,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetService<ILogger<DeployEventListener>>()));

        services.AddTransient<IEnvSource, FleetClientEnvSource>();
        services.AddTransient<IRestartTrigger, FleetClientRestartTrigger>();
        services.AddTransient<IStackEnvEditor, FleetClientStackEnvEditor>();
        return services;
    }

    public static IServiceCollection AddEggIdentityDeployFromEnvironment(this IServiceCollection services, string appName) {
        ArgumentNullException.ThrowIfNull(services);
        return DeployOptions.FromEnvironment(appName) is { } options ? services.AddEggIdentityDeploy(options) : services;
    }
}
