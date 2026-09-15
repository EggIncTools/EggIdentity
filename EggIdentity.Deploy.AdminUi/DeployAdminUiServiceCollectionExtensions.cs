using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Deploy.AdminUi;

public static class DeployAdminUiServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityDeployToasts(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<DeployToastBridge>();
        return services;
    }

    public static IServiceCollection AddEggIdentityPromotion(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IPromotionStore, SettingsPromotionStore>();
        services.AddScoped<PromotionService>();
        return services;
    }
}
