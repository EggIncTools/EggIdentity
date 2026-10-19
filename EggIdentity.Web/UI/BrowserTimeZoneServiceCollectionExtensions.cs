using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.UI;

public static class BrowserTimeZoneServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityBrowserTimeZone(this IServiceCollection services) {
        ViewerCookieOptions.In(services).Allow(BrowserTimeZone.CookieName, TimeSpan.FromDays(365));
        services.AddScoped<BrowserTimeZone>();
        return services;
    }
}
