using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggIdentity.Reports;

public interface IReportRowProvider<TRow> {
    ValueTask<IReadOnlyList<TRow>> ReadAsync(CancellationToken ct);

    ValueTask<string> VersionAsync(CancellationToken ct);
}

internal sealed class ReportSourceRegistration(Action<IServiceProvider, ReportEngineRegistry> apply) {
    public void Apply(IServiceProvider services, ReportEngineRegistry registry) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registry);
        apply(services, registry);
    }
}

public static class ReportServiceCollectionExtensions {
    public static IServiceCollection AddReportSource<TRow, TProvider>(
        this IServiceCollection services, ReportSource<TRow> source, SnapshotSchema? snapshot = null)
        where TProvider : class, IReportRowProvider<TRow> {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(source);
        ReportPresets.Check(source);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(BuildRegistry);
        services.AddScoped<TProvider>();
        services.AddSingleton<IReportSource>(source);
        services.AddSingleton(sp => new ReportSourceRegistration((provider, registry) =>
            RegisterEngines<TRow, TProvider>(provider, registry, source, snapshot)));

        return services;
    }

    private static ReportEngineRegistry BuildRegistry(IServiceProvider services) {
        var registry = new ReportEngineRegistry();
        foreach (var registration in services.GetServices<ReportSourceRegistration>()) registration.Apply(services, registry);
        return registry;
    }

    private static void RegisterEngines<TRow, TProvider>(
        IServiceProvider services, ReportEngineRegistry registry, ReportSource<TRow> source, SnapshotSchema? snapshot)
        where TProvider : class, IReportRowProvider<TRow> {
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var clock = services.GetRequiredService<TimeProvider>();

        registry.Add(source.Id, new InMemoryReportEngine<TRow>(source, ct => ReadAsync<TRow, TProvider>(scopeFactory, ct), clock));

        if (snapshot is not null) {
            registry.AddSnapshot(source.Id, new SnapshotReportEngine<TRow>(
                source, snapshot, ct => ReadAsync<TRow, TProvider>(scopeFactory, ct), ct => VersionAsync<TRow, TProvider>(scopeFactory, ct), clock));
        }
    }

    private static async ValueTask<IReadOnlyList<TRow>> ReadAsync<TRow, TProvider>(IServiceScopeFactory scopeFactory, CancellationToken ct)
        where TProvider : class, IReportRowProvider<TRow> {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TProvider>().ReadAsync(ct).ConfigureAwait(false);
    }

    private static async ValueTask<string> VersionAsync<TRow, TProvider>(IServiceScopeFactory scopeFactory, CancellationToken ct)
        where TProvider : class, IReportRowProvider<TRow> {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TProvider>().VersionAsync(ct).ConfigureAwait(false);
    }
}
