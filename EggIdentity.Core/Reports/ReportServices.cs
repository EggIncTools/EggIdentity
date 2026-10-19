using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggIdentity.Reports;

public interface IReportRowProvider<TRow> {
    ValueTask<IReadOnlyList<TRow>> ReadAsync(string? partition, CancellationToken ct);

    ValueTask<string> VersionAsync(string? partition, CancellationToken ct);
}

internal sealed class ReportSourceRegistration(Action<IServiceProvider, ReportEngineRegistry, bool> apply) {
    public void Apply(IServiceProvider services, ReportEngineRegistry registry, bool perScope) {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registry);
        apply(services, registry, perScope);
    }
}

public sealed record ReportSnapshotCapture(
    string Source,
    Func<IServiceProvider, string?, CancellationToken, ValueTask<string>> Version,
    Func<IServiceProvider, string?, string, CancellationToken, ValueTask<byte[]>> Capture,
    SnapshotSchema? Schema);

public static class ReportServiceCollectionExtensions {
    public static IServiceCollection AddReportSource<TRow, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
        this IServiceCollection services, ReportSource<TRow> source, SnapshotSchema? snapshot = null)
        where TProvider : class, IReportRowProvider<TRow> {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(source);
        ReportPresets.Check(source);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(BuildRegistry);
        services.AddScoped<TProvider>();
        services.AddSingleton<IReportSource>(source);
        services.AddSingleton(sp => new ReportSourceRegistration((provider, registry, perScope) =>
            RegisterEngines<TRow, TProvider>(provider, registry, source, snapshot, perScope)));
        services.AddSingleton(new ReportSnapshotCapture(
            source.Id,
            (provider, partition, ct) => VersionAsync<TRow, TProvider>(provider, partition, false, ct),
            (provider, partition, version, ct) => CaptureAsync<TRow, TProvider>(provider, source, partition, version, ct),
            snapshot));

        return services;
    }

    public static ReportEngineRegistry BuildScopedReportRegistry(this IServiceProvider scope) {
        ArgumentNullException.ThrowIfNull(scope);
        var registry = new ReportEngineRegistry();
        foreach (var registration in scope.GetServices<ReportSourceRegistration>()) registration.Apply(scope, registry, true);
        return registry;
    }

    private static ReportEngineRegistry BuildRegistry(IServiceProvider services) {
        var registry = new ReportEngineRegistry();
        foreach (var registration in services.GetServices<ReportSourceRegistration>()) registration.Apply(services, registry, false);
        return registry;
    }

    private static void RegisterEngines<TRow, TProvider>(
        IServiceProvider services, ReportEngineRegistry registry, ReportSource<TRow> source, SnapshotSchema? snapshot, bool perScope)
        where TProvider : class, IReportRowProvider<TRow> {
        var clock = services.GetRequiredService<TimeProvider>();

        registry.Add(source.Id, new InMemoryReportEngine<TRow>(
            source, (context, ct) => ReadAsync<TRow, TProvider>(services, context.Partition, perScope, ct), clock));

        if (snapshot is not null) {
            registry.AddSnapshot(source.Id, new SnapshotReportEngine<TRow>(
                source, snapshot,
                (context, ct) => ReadAsync<TRow, TProvider>(services, context.Partition, perScope, ct),
                (context, ct) => VersionAsync<TRow, TProvider>(services, context.Partition, perScope, ct),
                clock));
        }
    }

    private static async ValueTask<IReadOnlyList<TRow>> ReadAsync<TRow, TProvider>(IServiceProvider services, string? partition, bool perScope, CancellationToken ct)
        where TProvider : class, IReportRowProvider<TRow> {
        if (perScope) return await services.GetRequiredService<TProvider>().ReadAsync(partition, ct).ConfigureAwait(false);
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TProvider>().ReadAsync(partition, ct).ConfigureAwait(false);
    }

    private static async ValueTask<string> VersionAsync<TRow, TProvider>(IServiceProvider services, string? partition, bool perScope, CancellationToken ct)
        where TProvider : class, IReportRowProvider<TRow> {
        if (perScope) return await services.GetRequiredService<TProvider>().VersionAsync(partition, ct).ConfigureAwait(false);
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TProvider>().VersionAsync(partition, ct).ConfigureAwait(false);
    }

    private static async ValueTask<byte[]> CaptureAsync<TRow, TProvider>(IServiceProvider services, ReportSource<TRow> source, string? partition, string version, CancellationToken ct)
        where TProvider : class, IReportRowProvider<TRow> {
        var rows = await ReadAsync<TRow, TProvider>(services, partition, false, ct).ConfigureAwait(false);
        return ReportWire.WriteSnapshot(RowSnapshot.Capture(source, rows, version));
    }
}
