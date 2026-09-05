using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Deploy.AdminUi;

public static class DeployAdminUiServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityDeployToasts(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<DeployToastBridge>();
        return services;
    }
}
