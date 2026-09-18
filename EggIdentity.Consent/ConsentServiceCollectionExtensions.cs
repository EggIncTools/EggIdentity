using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Consent;

public static class ConsentServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityConsent(this IServiceCollection services, ConsentOptions options) {
        services.AddSingleton(options);
        services.AddScoped<ConsentReader>();
        services.AddScoped<IConsentReader>(sp => sp.GetRequiredService<ConsentReader>());
        return services;
    }
}
