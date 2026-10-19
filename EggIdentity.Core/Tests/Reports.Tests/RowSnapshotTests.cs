using EggIdentity.Filters;

using static EggIdentity.Reports.Tests.ReportFixture;

namespace EggIdentity.Reports.Tests;

public class RowSnapshotTests {
    private const string DropsSource = "drops";

    [Fact]
    public async Task Capture_ThenCreate_EvaluatesLikeTheLiveSource() {
        var query = new ReportQuery(DropsSource) {
            GroupBy = [new GroupKey("ship"), new GroupKey("at", TimeBucket.Month)],
            Measures = [new MeasureRequest("value", Aggregation.Sum), new MeasureRequest("mission", Aggregation.Distinct)],
            Filters = new FilterSet([new FilterCondition("rarity", FilterOps.Gte, "1")], []),
        };

        var expected = await Engine().RunAsync(query, ReportContext.Utc, CancellationToken.None);

        var (descriptor, downgrades) = SourceDescriptor.Capture(Source);
        Assert.Empty(downgrades);
        var snapshot = RowSnapshot.Capture(Source, Rows, "v1");
        var snapshotSource = SnapshotSource.Create(descriptor, snapshot);
        var indexEngine = new InMemoryReportEngine<int>(
            snapshotSource, _ => ValueTask.FromResult<IReadOnlyList<int>>([.. Enumerable.Range(0, Rows.Length)]), new FixedClock(Now));
        var actual = await indexEngine.RunAsync(query, ReportContext.Utc, CancellationToken.None);

        Assert.Equal(expected.Dimensions, actual.Dimensions);
        Assert.Equal(expected.Measures, actual.Measures);
        Assert.Equal(expected.Rows.Count, actual.Rows.Count);
        foreach (var expectedRow in expected.Rows) {
            var actualRow = actual.Rows.Single(r => r.Keys.SequenceEqual(expectedRow.Keys, StringComparer.Ordinal));
            Assert.Equal(expectedRow.Values, actualRow.Values);
            Assert.Equal(expectedRow.Samples, actualRow.Samples);
        }
    }

    [Fact]
    public void Capture_PreservesCustomOrderOverOptions() {
        var field = new FilterFieldDef("ship", "Ship", FilterValueKind.Select, FilterOps.Comparison) {
            Options = [new("henerprise", "Henerprise"), new("atreggies", "Atreggies"), new("chicken_one", "Chicken One")],
        };
        var reversed = new ReportDimension(field) {
            Order = DimensionOrder.Custom((a, b) => string.CompareOrdinal(b, a)),
        };
        var source = Source with { Dimensions = [reversed, Item, Rarity, At] };

        var (descriptor, downgrades) = SourceDescriptor.Capture(source);
        Assert.DoesNotContain("ship", downgrades);
        var shipDescriptor = descriptor.Dimensions.Single(d => d.Field == "ship");
        Assert.Equal(["henerprise", "chicken_one", "atreggies"], shipDescriptor.CustomOrder);

        var snapshot = RowSnapshot.Capture(source, Rows, "v1");
        var snapshotSource = SnapshotSource.Create(descriptor, snapshot);
        var shipDimension = snapshotSource.Dimension("ship") ?? throw new Xunit.Sdk.XunitException("missing ship dimension");
        var keys = new[] { "henerprise", "chicken_one", "atreggies", "unknown" };
        var sorted = keys.OrderBy(k => k, Comparer<string>.Create(shipDimension.Compare)).ToList();
        Assert.Equal(["henerprise", "chicken_one", "atreggies", "unknown"], sorted);
    }

    [Fact]
    public void Capture_ReportsCustomOrderDowngradeWithoutOptions() {
        var field = new FilterFieldDef("item", "Item", FilterValueKind.Select, FilterOps.Equality) { Scope = "artifact" };
        var customNoOptions = new ReportDimension(field) {
            Order = DimensionOrder.Custom((a, b) => string.CompareOrdinal(a, b)),
        };
        var source = Source with { Dimensions = [Ship, customNoOptions, Rarity, At] };

        var (descriptor, downgrades) = SourceDescriptor.Capture(source);
        Assert.Contains("item", downgrades);
        var itemDescriptor = descriptor.Dimensions.Single(d => d.Field == "item");
        Assert.Null(itemDescriptor.CustomOrder);
    }

    [Fact]
    public void Column_At_ReturnsAbsentWhereBitmapIsClear() {
        var numberColumn = new SnapshotColumn("number", FilterValueKind.Number, 3) { Numbers = [1.0, 0.0, 3.0] };
        numberColumn.Mark(0);
        numberColumn.Mark(2);
        Assert.Equal(FilterSubjectValue.Of(1.0), numberColumn.At(0));
        Assert.Equal(FilterSubjectValue.Absent, numberColumn.At(1));
        Assert.Equal(FilterSubjectValue.Of(3.0), numberColumn.At(2));

        var textColumn = new SnapshotColumn("text", FilterValueKind.Text, 2) { Dictionary = ["a"], Text = [0, 0] };
        textColumn.Mark(0);
        Assert.Equal(FilterSubjectValue.Of("a"), textColumn.At(0));
        Assert.Equal(FilterSubjectValue.Absent, textColumn.At(1));

        var manyColumn = new SnapshotColumn("many", FilterValueKind.Set, 2) {
            Dictionary = ["a", "b"],
            ManyOffsets = [0, 2, 2],
            ManyItems = [0, 1],
        };
        manyColumn.Mark(0);
        var many = Assert.IsType<FilterSubjectValue.ManyValue>(manyColumn.At(0));
        Assert.Equal(["a", "b"], many.Values);
        Assert.Equal(FilterSubjectValue.Absent, manyColumn.At(1));
    }
}
