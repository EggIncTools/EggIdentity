using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.UI;

public static class BrowserTimeZoneServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityBrowserTimeZone(this IServiceCollection services) {
        services.AddHttpContextAccessor();
        services.AddScoped<BrowserTimeZone>();
        return services;
    }
}
