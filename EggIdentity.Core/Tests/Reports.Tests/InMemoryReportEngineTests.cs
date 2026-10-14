using EggIdentity.Filters;
using static EggIdentity.Reports.Tests.ReportFixture;

namespace EggIdentity.Reports.Tests;

public class InMemoryReportEngineTests {
    private static ReportQuery Query(params GroupKey[] groupBy) => new("drops") { GroupBy = groupBy };

    [Fact]
    public async Task NoGroupBy_CountsEveryRow() {
        var cube = await Run(Query());
        var row = Assert.Single(cube.Rows);
        Assert.Equal(5d, row.Values[0]);
        Assert.Equal(4, row.Samples);
    }

    [Fact]
    public async Task SingleDimension_CountsAndOrdersByDeclaredOptions() {
        var cube = await Run(Query(new GroupKey("ship")));
        Assert.Equal(["henerprise", "atreggies", "chicken_one"], cube.Rows.Select(r => r.Keys[0]));
        Assert.Equal(new double?[] { 3, 1, 1 }, cube.Rows.Select(r => r.Values[0]));
    }

    [Fact]
    public async Task Samples_CountDistinctSampleKey() {
        var cube = await Run(Query(new GroupKey("ship")));
        Assert.Equal(2, Row(cube, "henerprise").Samples);
    }

    [Fact]
    public async Task TwoDimensions_GroupEveryObservedPair() {
        var cube = await Run(Query(new GroupKey("ship"), new GroupKey("item")));
        Assert.Equal(4, cube.Rows.Count);
        Assert.Equal(2d, Row(cube, "henerprise", "gusset").Values[0]);
        Assert.Equal(1d, Row(cube, "henerprise", "feather").Values[0]);
        Assert.Equal(1d, Row(cube, "chicken_one", "feather").Values[0]);
    }

    [Fact]
    public async Task ThreeDimensions_KeyEveryAxis() {
        var cube = await Run(Query(new GroupKey("ship"), new GroupKey("item"), new GroupKey("rarity")));
        Assert.Equal(["ship", "item", "rarity"], cube.Dimensions);
        Assert.Equal(1d, Row(cube, "henerprise", "gusset", "3").Values[0]);
        Assert.Equal(1d, Row(cube, "henerprise", "gusset", "1").Values[0]);
    }

    [Fact]
    public async Task Aggregations_ComputeOverNumericValues() {
        var query = Query(new GroupKey("ship")) with {
            Measures = [new("value", Aggregation.Sum), new("value", Aggregation.Avg), new("value", Aggregation.Min), new("value", Aggregation.Max)],
        };
        var row = Row(await Run(query), "henerprise");
        Assert.Equal(32d, row.Values[0]);
        Assert.Equal(32d / 3, row.Values[1]!.Value, 9);
        Assert.Equal(5d, row.Values[2]);
        Assert.Equal(15d, row.Values[3]);
    }

    [Fact]
    public async Task Distinct_CountsDistinctValues() {
        var cube = await Run(Query(new GroupKey("item")) with { Measures = [new("mission", Aggregation.Distinct)] });
        Assert.Equal(3d, Row(cube, "gusset").Values[0]);
        Assert.Equal(2d, Row(cube, "feather").Values[0]);
    }

    [Fact]
    public async Task DistinctBy_SumsOneValuePerEntity() {
        var cube = await Run(Query(new GroupKey("ship")) with { Measures = [new("airtime", Aggregation.Sum)] });
        Assert.Equal(180d, Row(cube, "henerprise").Values[0]);
    }

    [Fact]
    public async Task DistributionMeasure_BinsValues() {
        var cube = await Run(Query(new GroupKey("ship")) with { Measures = [new("value", Aggregation.Sum)] });
        var dist = Row(cube, "henerprise").Distributions[0];
        Assert.NotNull(dist);
        Assert.Equal([0d, 10d], dist.Buckets.Select(b => b.Value));
        Assert.Equal([1, 2], dist.Buckets.Select(b => b.Count));
        Assert.Equal(2d / 3, dist.Buckets[1].Share, 9);
    }

    [Fact]
    public async Task WeekBucket_KeysByMondayStart() {
        var cube = await Run(Query(new GroupKey("at", TimeBucket.Week, FillGaps: false)));
        Assert.Equal(3d, Row(cube, "2026-10-05").Values[0]);
        Assert.Equal(1d, Row(cube, "2026-09-21").Values[0]);
        Assert.Equal(1d, Row(cube, "2026-02-23").Values[0]);
    }

    [Fact]
    public async Task WeekBucket_UsesContextZone() {
        var plusTwo = TimeZoneInfo.CreateCustomTimeZone("plus2", TimeSpan.FromHours(2), "plus2", "plus2");
        var cube = await Engine().RunAsync(Query(new GroupKey("at", TimeBucket.Week, FillGaps: false)), new ReportContext(plusTwo), CancellationToken.None);
        Assert.Equal(2d, Row(cube, "2026-10-05").Values[0]);
        Assert.Equal(1d, Row(cube, "2026-10-12").Values[0]);
    }

    [Fact]
    public async Task MonthBucket_FillsGapsBetweenFirstAndLast() {
        var cube = await Run(Query(new GroupKey("at", TimeBucket.Month)));
        Assert.Equal(8, cube.Rows.Count);
        Assert.Equal(0d, Row(cube, "2026-04").Values[0]);
        Assert.Equal(0, Row(cube, "2026-04").Samples);
        Assert.Equal(3d, Row(cube, "2026-10").Values[0]);
    }

    [Fact]
    public async Task GapFill_SpansTheWithinFilterRange() {
        var query = Query(new GroupKey("at", TimeBucket.Week)) with { Filters = new FilterSet([new("at", FilterOps.Within, "30d")], []) };
        var cube = await Run(query);
        Assert.Equal(["2026-09-14", "2026-09-21", "2026-09-28", "2026-10-05", "2026-10-12"], cube.Rows.Select(r => r.Keys[0]));
        Assert.Equal(new double?[] { 0, 1, 0, 3, 0 }, cube.Rows.Select(r => r.Values[0]));
    }

    [Fact]
    public async Task GapFill_PadsEverySeriesOfTheOtherAxis() {
        var cube = await Run(Query(new GroupKey("ship"), new GroupKey("at", TimeBucket.Month)));
        Assert.Equal(24, cube.Rows.Count);
        Assert.Equal(0d, Row(cube, "atreggies", "2026-03").Values[0]);
    }

    [Fact]
    public async Task Within_FiltersByEngineClock() {
        var query = Query() with { Filters = new FilterSet([new("at", FilterOps.Within, "1w")], []) };
        Assert.Equal(1d, Assert.Single((await Run(query)).Rows).Values[0]);
    }

    [Fact]
    public async Task NormalizeBy_DividesByDenominatorPerGroup() {
        var cube = await Run(Query(new GroupKey("ship")) with { Normalize = Normalize.By("airtime") });
        Assert.True(cube.Normalized);
        Assert.Equal(3d / 180, Row(cube, "henerprise").Values[0]!.Value, 9);
    }

    [Fact]
    public async Task NormalizeBy_SkipsWhenDimensionScopeIsOutsideDenominator() {
        var cube = await Run(Query(new GroupKey("item")) with { Normalize = Normalize.By("airtime") });
        Assert.False(cube.Normalized);
        Assert.Equal(3d, Row(cube, "gusset").Values[0]);
    }

    [Fact]
    public async Task NormalizeRow_SharesWithinFirstAxis() {
        var cube = await Run(Query(new GroupKey("ship"), new GroupKey("item")) with { Normalize = Normalize.Row });
        Assert.True(cube.Normalized);
        Assert.Equal(200d / 3, Row(cube, "henerprise", "gusset").Values[0]!.Value, 9);
        Assert.Equal(100d, Row(cube, "atreggies", "gusset").Values[0]);
    }

    [Fact]
    public async Task NormalizeGlobal_SharesAcrossCube() {
        var cube = await Run(Query(new GroupKey("ship")) with { Normalize = Normalize.Global });
        Assert.Equal(60d, Row(cube, "henerprise").Values[0]!.Value, 9);
    }

    [Fact]
    public async Task Having_DropsGroupsFailingTheThreshold() {
        var cube = await Run(Query(new GroupKey("ship")) with { Having = [new("count", FilterOps.Gte, 2)] });
        Assert.Equal("henerprise", Assert.Single(cube.Rows).Keys[0]);
    }

    [Fact]
    public async Task Sort_ByMeasureThenDimensionOrder() {
        var cube = await Run(Query(new GroupKey("item")) with { Sort = [new("count", Descending: false)] });
        Assert.Equal(["feather", "gusset"], cube.Rows.Select(r => r.Keys[0]));
    }

    [Fact]
    public async Task Limit_KeepsTopNOfFirstAxisWithAllTheirCells() {
        var cube = await Run(Query(new GroupKey("ship"), new GroupKey("item")) with { Limit = 1, Sort = [new("count", Descending: true)] });
        Assert.Equal(2, cube.Rows.Count);
        Assert.All(cube.Rows, r => Assert.Equal("henerprise", r.Keys[0]));
    }

    [Fact]
    public async Task MinSample_DoesNotFilterRows() {
        var cube = await Run(Query(new GroupKey("ship")) with { MinSample = 5 });
        Assert.Equal(3, cube.Rows.Count);
    }

    [Fact]
    public async Task UnknownDimension_Throws() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Run(Query(new GroupKey("nope"))));
}
