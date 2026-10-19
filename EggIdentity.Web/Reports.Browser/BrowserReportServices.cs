using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggIdentity.Reports.Browser;

public static class BrowserReportServiceCollectionExtensions {
    public static IServiceCollection AddEggIdentityBrowserReports(this IServiceCollection services, Action<BrowserReportOptions>? configure = null) {
        ArgumentNullException.ThrowIfNull(services);

        var options = new BrowserReportOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SourceDescriptorCatalog>();
        services.AddSingleton<SnapshotCache>();
        services.AddScoped<BrowserReportRegistry>();
        services.AddHttpContextAccessor();

        return services;
    }
}
