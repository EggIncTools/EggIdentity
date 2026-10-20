using System.Text.Json.Nodes;

namespace EggIdentity.Reports.Tests;

public class ReportNumberFormatTests {
    private static ReportDefinition Definition(ReportNumberFormat? format) =>
        new(1, "r1", "t", new ReportQuery("drops") { GroupBy = [new GroupKey("ship")] }, new ReportView(ReportCharts.Bar) { NumberFormat = format });

    [Fact]
    public void CompactKindFormatsAndAxesThroughTheSharedRule() {
        Assert.Equal("60.9k", ValueFormat.Compact.Format(60907));
        Assert.Equal("60.9k", ValueFormat.Compact.Axis(60907));
    }

    [Fact]
    public void NullOverrideIsOmittedAndRoundTrips() {
        var json = JsonNode.Parse(ReportCodec.Serialize(Definition(null)))!["view"]!.AsObject();
        var back = ReportCodec.Deserialize(ReportCodec.Serialize(Definition(ReportNumberFormat.Compact)));

        Assert.False(json.ContainsKey("numberFormat"));
        Assert.Equal(ReportNumberFormat.Compact, back.View.NumberFormat);
    }

    [Theory]
    [InlineData(ReportNumberFormat.Compact, ValueFormatKind.Integer, ValueFormatKind.Compact)]
    [InlineData(ReportNumberFormat.Compact, ValueFormatKind.Decimal, ValueFormatKind.Compact)]
    [InlineData(ReportNumberFormat.Compact, ValueFormatKind.Percent, ValueFormatKind.Percent)]
    [InlineData(ReportNumberFormat.Full, ValueFormatKind.Compact, ValueFormatKind.Integer)]
    [InlineData(ReportNumberFormat.Full, ValueFormatKind.Duration, ValueFormatKind.Duration)]
    public void OverrideNeverTouchesPercentOrDuration(ReportNumberFormat over, ValueFormatKind measure, ValueFormatKind expected) =>
        Assert.Equal(expected, Definition(over).View.Resolve(new ValueFormat(measure)).Kind);

    [Fact]
    public void OneKeyOneMeasureOffersPie() =>
        Assert.Equal([ReportCharts.Bar, ReportCharts.Pie, ReportCharts.Line, ReportCharts.Table], ReportCharts.Compatible(Definition(null).Query));

    [Fact]
    public void PieDropsForNegativesOrSeveralMeasures() {
        var multi = Definition(null).Query with { Measures = [new MeasureRequest("a", Aggregation.Sum), new MeasureRequest("b", Aggregation.Sum)] };

        Assert.DoesNotContain(ReportCharts.Pie, ReportCharts.Compatible(Definition(null).Query, hasNegative: true));
        Assert.DoesNotContain(ReportCharts.Pie, ReportCharts.Compatible(multi));
    }

    [Fact]
    public void TwoKeysOfferSeriesCharts() {
        var two = Definition(null).Query with { GroupBy = [new GroupKey("ship"), new GroupKey("level")] };

        Assert.Equal([ReportCharts.GroupedBar, ReportCharts.MultiLine, ReportCharts.Heatmap, ReportCharts.Table], ReportCharts.Compatible(two));
        Assert.Equal([ReportCharts.Table], ReportCharts.Compatible(Definition(null).Query with { GroupBy = [] }));
    }

    [Fact]
    public void CubeOverloadSeesNegativeCells() {
        var cube = new ReportCube(["ship"], [MeasureRequest.RowCount], false, [new CubeRow(["a"], [-1.0], 1)]);

        Assert.DoesNotContain(ReportCharts.Pie, ReportCharts.Compatible(Definition(null).Query, cube));
    }
}
