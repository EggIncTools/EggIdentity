using EggIdentity.Settings.AdminUi;
using EggIdentity.Settings.Api;
using EggIdentity.Settings.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggIdentity.Suite.AdminUi;

public static class SuiteAdminServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentitySuiteAdmin(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient();
        services.AddEggIdentitySettingsPanel();
        services.TryAddSingleton(sp => new AdminApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
        services.AddScoped(sp => new SuiteAdmin(
            sp.GetRequiredService<SettingsAdminService>(),
            sp.GetRequiredService<AdminApiClient>(),
            sp.GetRequiredService<IHttpClientFactory>()));
        return services;
    }
}
