using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.UI;

public static class ToastServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityToasts(this IServiceCollection services) {
        services.AddScoped<ToastService>();
        return services;
    }
}
