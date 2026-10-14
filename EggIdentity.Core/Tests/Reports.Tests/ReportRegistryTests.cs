using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Reports.Tests;

public class ReportRegistryTests {
    [Fact]
    public async Task DimensionCapRejectsWithoutRunning() {
        var engine = new CountingEngine();
        var registry = new ReportEngineRegistry().Add(ReportFixture.Source.Id, engine);
        registry.SetCaps(ReportTier.Live, new ReportTierCaps(1, int.MaxValue, int.MaxValue));
        var query = new ReportQuery(ReportFixture.Source.Id) { GroupBy = [new GroupKey("ship"), new GroupKey("item")] };

        var ex = await Assert.ThrowsAsync<ReportRejectedException>(() => registry.RunAsync(query, ReportContext.Utc, CancellationToken.None));

        Assert.Contains(ex.Rejections, r => r.Kind == ReportRejectionKind.DimensionCap);
        Assert.Equal(0, engine.Runs);
    }

    [Fact]
    public async Task CellCapRejectsAfterRun() {
        var registry = new ReportEngineRegistry().Add(ReportFixture.Source.Id, ReportFixture.Engine());
        registry.SetCaps(ReportTier.Live, new ReportTierCaps(int.MaxValue, 1, int.MaxValue));
        var query = new ReportQuery(ReportFixture.Source.Id) { GroupBy = [new GroupKey("ship")] };

        var ex = await Assert.ThrowsAsync<ReportRejectedException>(() => registry.RunAsync(query, ReportContext.Utc, CancellationToken.None));

        Assert.Contains(ex.Rejections, r => r.Kind == ReportRejectionKind.CellCap);
    }

    [Fact]
    public async Task UnknownSourceThrowsKeyNotFound() {
        var registry = new ReportEngineRegistry();
        var query = new ReportQuery("missing");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => registry.RunAsync(query, ReportContext.Utc, CancellationToken.None));
    }

    [Fact]
    public async Task SnapshotTierWithNoSnapshotEngineRejectsNoSnapshot() {
        var registry = new ReportEngineRegistry().Add(ReportFixture.Source.Id, ReportFixture.Engine());
        var query = new ReportQuery(ReportFixture.Source.Id);
        var context = new ReportContext(TimeZoneInfo.Utc, ReportTier.Snapshot);

        var ex = await Assert.ThrowsAsync<ReportRejectedException>(() => registry.RunAsync(query, context, CancellationToken.None));

        Assert.Contains(ex.Rejections, r => r.Kind == ReportRejectionKind.NoSnapshot);
    }

    [Fact]
    public async Task DiResolvesFreshScopedProviderPerRun() {
        var before = CountingProvider.Constructions;
        var services = new ServiceCollection();
        services.AddReportSource<Drop, CountingProvider>(ReportFixture.Source);
        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ReportEngineRegistry>();
        var query = new ReportQuery(ReportFixture.Source.Id);

        await registry.RunAsync(query, ReportContext.Utc, CancellationToken.None);
        await registry.RunAsync(query, ReportContext.Utc, CancellationToken.None);

        Assert.Equal(before + 2, CountingProvider.Constructions);
    }

    private sealed class CountingEngine : IReportEngine {
        public int Runs { get; private set; }

        public Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
            Runs++;
            return Task.FromResult(new ReportCube([], [], false, []));
        }
    }

    private sealed class CountingProvider : IReportRowProvider<Drop> {
        public static int Constructions;

        public CountingProvider() {
            Constructions++;
        }

        public ValueTask<IReadOnlyList<Drop>> ReadAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<Drop>>(ReportFixture.Rows);

        public ValueTask<string> VersionAsync(CancellationToken ct) => ValueTask.FromResult("v1");
    }
}
