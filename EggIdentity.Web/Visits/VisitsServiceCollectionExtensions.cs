using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggIdentity.Visits;

public static class VisitsServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityVisits(this IServiceCollection services, VisitsOptions options) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Site);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(options);
        services.AddSingleton<VisitorHasher>();
        services.AddSingleton<VisitTracker>();
        services.AddSingleton<VisitsStore>();
        services.AddSingleton<IVisitsSink>(sp => sp.GetRequiredService<VisitsStore>());
        services.AddHostedService<VisitFlusher>();
        return services;
    }
}
