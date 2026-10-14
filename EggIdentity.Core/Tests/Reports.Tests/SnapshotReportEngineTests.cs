using EggIdentity.Filters;

using static EggIdentity.Reports.Tests.ReportFixture;

namespace EggIdentity.Reports.Tests;

public class SnapshotReportEngineTests {
    private const string DropsSource = "drops";
    private const string AirtimeKey = "airtime";
    private const string ValueKey = "value";
    private const string MissionKey = "mission";

    private static readonly SnapshotSchema Grain = new(DropsSource, [new GroupKey("ship"), new GroupKey("item"), new GroupKey("rarity"), new GroupKey("at", TimeBucket.Day)], [AirtimeKey, ValueKey, MissionKey]);

    private static SnapshotReportEngine<Drop> Snapshot(SnapshotSchema? schema = null, Func<CancellationToken, ValueTask<IReadOnlyList<Drop>>>? rows = null, Func<CancellationToken, ValueTask<string>>? version = null) =>
        new(Source, schema ?? Grain, rows ?? (_ => ValueTask.FromResult<IReadOnlyList<Drop>>(Rows)), version ?? (_ => ValueTask.FromResult("v1")), new FixedClock(Now));

    private static async Task AssertParity(ReportQuery query) {
        var ct = CancellationToken.None;
        var expected = await Engine().RunAsync(query, ReportContext.Utc, ct);
        var actual = await Snapshot().RunAsync(query, ReportContext.Utc, ct);
        Assert.Equal(expected.Dimensions, actual.Dimensions);
        Assert.Equal(expected.Measures, actual.Measures);
        Assert.Equal(expected.Rows.Count, actual.Rows.Count);
        foreach (var expectedRow in expected.Rows) {
            var actualRow = actual.Rows.Single(r => r.Keys.SequenceEqual(expectedRow.Keys, StringComparer.Ordinal));
            AssertValues(expectedRow.Values, actualRow.Values);
            Assert.Equal(expectedRow.Samples, actualRow.Samples);
            AssertDistributions(expectedRow.Distributions, actualRow.Distributions);
        }
    }

    private static void AssertValues(IReadOnlyList<double?> expected, IReadOnlyList<double?> actual) {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++) {
            AssertNullableDoubleEqual(expected[i], actual[i]);
        }
    }

    private static void AssertNullableDoubleEqual(double? expected, double? actual) {
        if (expected is null || actual is null) {
            Assert.Equal(expected, actual);
            return;
        }
        Assert.Equal(expected.Value, actual.Value, 9);
    }

    private static void AssertDistributions(IReadOnlyList<Distribution?> expected, IReadOnlyList<Distribution?> actual) {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++) {
            if (expected[i] is not { } e || actual[i] is not { } a) {
                Assert.Equal(expected[i] is null, actual[i] is null);
                continue;
            }
            var expectedBuckets = e.Buckets;
            var actualBuckets = a.Buckets;
            Assert.Equal(expectedBuckets.Count, actualBuckets.Count);
            for (var j = 0; j < expectedBuckets.Count; j++) {
                Assert.Equal(expectedBuckets[j].Value, actualBuckets[j].Value, 9);
                Assert.Equal(expectedBuckets[j].Count, actualBuckets[j].Count);
                Assert.Equal(expectedBuckets[j].Share, actualBuckets[j].Share, 9);
            }
        }
    }

    [Fact]
    public async Task Parity_CountByShip() {
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship")],
        };
        await AssertParity(query);
    }

    [Fact]
    public async Task Parity_SumAvgMinMaxValueByShipAndItem() {
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship"), new GroupKey("item")],
            Measures = [
                new MeasureRequest(ValueKey, Aggregation.Sum),
                new MeasureRequest(ValueKey, Aggregation.Avg),
                new MeasureRequest(ValueKey, Aggregation.Min),
                new MeasureRequest(ValueKey, Aggregation.Max),
            ],
        };
        await AssertParity(query);
    }

    [Fact]
    public async Task Parity_DistributionByItem() {
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("item")],
            Measures = [new MeasureRequest(ValueKey, Aggregation.Sum)],
        };
        await AssertParity(query);
    }

    [Fact]
    public async Task Parity_MonthFromDayGrain() {
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("at", TimeBucket.Month, FillGaps: false)],
        };
        await AssertParity(query);
    }

    [Fact]
    public async Task Parity_FilterOnGrainDim() {
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("item")],
            Filters = new FilterSet([new FilterCondition("ship", FilterOps.Eq, "henerprise")], []),
        };
        await AssertParity(query);
    }

    [Fact]
    public async Task Parity_AirtimeDistinctByMission() {
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship")],
            Measures = [new MeasureRequest(AirtimeKey, Aggregation.Sum)],
        };
        await AssertParity(query);
    }

    [Fact]
    public void Rejects_NonGrainDimension() {
        var schema = new SnapshotSchema(DropsSource, [new GroupKey("ship")], [AirtimeKey, ValueKey, MissionKey]);
        var engine = Snapshot(schema);
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("item")],
        };
        var validation = engine.Validate(query, ReportTierCaps.Unlimited);
        Assert.False(validation.Allowed);
        Assert.Contains(validation.Rejections, r => r.Kind == ReportRejectionKind.DimensionNotInGrain && r.Key == "item");
    }

    [Fact]
    public void Rejects_NonGrainFilter() {
        var schema = new SnapshotSchema(DropsSource, [new GroupKey("ship")], [AirtimeKey, ValueKey, MissionKey]);
        var engine = Snapshot(schema);
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship")],
            Filters = new FilterSet([new FilterCondition("item", FilterOps.Eq, "gusset")], []),
        };
        var validation = engine.Validate(query, ReportTierCaps.Unlimited);
        Assert.False(validation.Allowed);
        Assert.Contains(validation.Rejections, r => r.Kind == ReportRejectionKind.FilterNotInGrain && r.Key == "item");
    }

    [Fact]
    public void Rejects_WeekToMonth() {
        var schema = new SnapshotSchema(DropsSource, [new GroupKey("ship"), new GroupKey("at", TimeBucket.Week)], [AirtimeKey, ValueKey, MissionKey]);
        var engine = Snapshot(schema);
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("at", TimeBucket.Month)],
        };
        var validation = engine.Validate(query, ReportTierCaps.Unlimited);
        Assert.False(validation.Allowed);
        Assert.Contains(validation.Rejections, r => r.Kind == ReportRejectionKind.BucketNotDerivable && r.Key == "at");
    }

    [Fact]
    public void Rejects_DistinctOnNonGrainKey() {
        var schema = new SnapshotSchema(DropsSource, [new GroupKey("ship")], [MissionKey]);
        var engine = Snapshot(schema);
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship")],
            Measures = [new MeasureRequest(MissionKey, Aggregation.Distinct)],
        };
        var validation = engine.Validate(query, ReportTierCaps.Unlimited);
        Assert.False(validation.Allowed);
        Assert.Contains(validation.Rejections, r => r.Kind == ReportRejectionKind.MeasureNotMergeable && r.Key == MissionKey);
    }

    [Fact]
    public async Task Run_InvalidQuery_Throws() {
        var schema = new SnapshotSchema(DropsSource, [new GroupKey("ship")], [AirtimeKey, ValueKey, MissionKey]);
        var engine = Snapshot(schema);
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("item")],
        };
        await Assert.ThrowsAsync<ReportRejectedException>(() => engine.RunAsync(query, ReportContext.Utc, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentRuns_BuildOnce() {
        var calls = 0;
        var engine = Snapshot(rows: async ct => {
            Interlocked.Increment(ref calls);
            await Task.Delay(50, ct);
            return Rows;
        });
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship")],
        };
        var tasks = Enumerable.Range(0, 5).Select(_ => engine.RunAsync(query, ReportContext.Utc, CancellationToken.None));
        await Task.WhenAll(tasks);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task VersionChange_Rebuilds() {
        var calls = 0;
        var version = "v1";
        var engine = Snapshot(
            rows: ct => {
                Interlocked.Increment(ref calls);
                return ValueTask.FromResult<IReadOnlyList<Drop>>(Rows);
            },
            version: _ => ValueTask.FromResult(version));
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship")],
        };
        await engine.RunAsync(query, ReportContext.Utc, CancellationToken.None);
        version = "v2";
        await engine.RunAsync(query, ReportContext.Utc, CancellationToken.None);
        Assert.Equal(2, calls);
        Assert.Equal("v2", engine.Version);
    }

    [Fact]
    public async Task FailedRebuild_ServesPrevious() {
        var first = true;
        var engine = Snapshot(rows: ct => {
            if (first) {
                first = false;
                return ValueTask.FromResult<IReadOnlyList<Drop>>(Rows);
            }
            throw new InvalidOperationException();
        });
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship")],
        };
        var initial = await engine.RunAsync(query, ReportContext.Utc, CancellationToken.None);
        engine.Invalidate();
        var after = await engine.RunAsync(query, ReportContext.Utc, CancellationToken.None);
        Assert.Equal(initial.Rows.Count, after.Rows.Count);
    }
}
