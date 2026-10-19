namespace EggIdentity.Reports.Browser.Tests;

public class HookedReportEngineTests {
    private static readonly ReportContext Context = new(TimeZoneInfo.Utc);

    private static ReportCube Cube(double value) =>
        new(["ship"], [MeasureRequest.RowCount], false, [new CubeRow(["henerprise"], [value], 1)]);

    private sealed class FixedEngine(Func<ReportQuery, ReportCube> run) : IReportEngine {
        public List<ReportQuery> Seen { get; } = [];

        public Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
            Seen.Add(query);
            return Task.FromResult(run(query));
        }
    }

    private sealed class UnavailableEngine : IReportEngine {
        public Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) =>
            throw new BrowserUnavailableException("unavailable: no worker");
    }

    private static HookedReportEngine Engine(IReportEngine browser, IReportEngine? server, ReportHooks hooks, Func<bool> active, Action<BrowserUnavailableException> onFallback) =>
        new(_ => browser, _ => server, q => hooks.Prepare?.Invoke(q) ?? q, hooks.PostProcess, active, onFallback);

    [Fact]
    public async Task HookedEngine_AppliesPrepareAndPostProcess() {
        var primary = new FixedEngine(_ => Cube(1));
        var hooks = new ReportHooks(
            Prepare: q => q with { Limit = 7 },
            PostProcess: (cube, _, _) => Task.FromResult(cube with { Normalized = true }));
        var engine = Engine(primary, null, hooks, () => true, _ => { });

        var result = await engine.RunAsync(new ReportQuery("drops"), Context, CancellationToken.None);

        Assert.Equal(7, Assert.Single(primary.Seen).Limit);
        Assert.True(result.Normalized);
    }

    [Fact]
    public async Task HookedEngine_FallsBackOnUnavailable() {
        var fallback = new FixedEngine(_ => Cube(2));
        var fellBack = false;
        var engine = Engine(new UnavailableEngine(), fallback, new ReportHooks(), () => true, _ => fellBack = true);

        var result = await engine.RunAsync(new ReportQuery("drops"), Context, CancellationToken.None);

        Assert.True(fellBack);
        Assert.Equal(2, result.Rows[0].Values[0]);
    }

    [Fact]
    public async Task HookedEngine_SkipsBrowserWhenInactive() {
        var browser = new FixedEngine(_ => Cube(1));
        var server = new FixedEngine(_ => Cube(2));
        var engine = Engine(browser, server, new ReportHooks(), () => false, _ => { });

        var result = await engine.RunAsync(new ReportQuery("drops"), Context, CancellationToken.None);

        Assert.Empty(browser.Seen);
        Assert.Equal(2, result.Rows[0].Values[0]);
    }

    [Fact]
    public async Task HookedEngine_DispatchesOnPreparedSource() {
        var missions = new FixedEngine(_ => Cube(3));
        var engine = new HookedReportEngine(
            s => s == "missions" ? missions : null,
            _ => null,
            q => q with { Source = "missions" },
            null,
            () => true,
            _ => { });

        var result = await engine.RunAsync(new ReportQuery("artifacts"), Context, CancellationToken.None);

        Assert.Equal("missions", Assert.Single(missions.Seen).Source);
        Assert.Equal(3, result.Rows[0].Values[0]);
    }

    [Fact]
    public async Task HookedEngine_RethrowsUnavailableWithoutFallback() {
        var engine = Engine(new UnavailableEngine(), null, new ReportHooks(), () => true, _ => { });

        await Assert.ThrowsAsync<BrowserUnavailableException>(() => engine.RunAsync(new ReportQuery("drops"), Context, CancellationToken.None));
    }
}
