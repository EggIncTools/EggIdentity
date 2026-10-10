using EggIdentity.UI;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Settings.AdminUi;

public static class SettingsAdminUiServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentitySettingsPanel(this IServiceCollection services) {
        ArgumentNullException.ThrowIfNull(services);
        services.AddEggIdentityToasts();
        return services;
    }
}
