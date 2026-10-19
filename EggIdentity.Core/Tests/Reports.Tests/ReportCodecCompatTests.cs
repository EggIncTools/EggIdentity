using EggIdentity.Filters;

namespace EggIdentity.Reports.Tests;

public class ReportCodecCompatTests {
    private static ReportDefinition Sample() => new(1, "r1", "Drops by ship", new ReportQuery("drops") {
        GroupBy = [new GroupKey("ship", TimeBucket.Month, FillGaps: false)],
        Measures = [new MeasureRequest("value", Aggregation.Sum)],
        Filters = new FilterSet([new("at", FilterOps.Within, "30d")], [[new("ship", "=", "henerprise")]]),
        Normalize = Normalize.By("airtime"),
        Sort = [new SortKey("value", Descending: true)],
        Having = [new HavingClause("value", FilterOps.Gt, 2)],
        Limit = 5,
        MinSample = 3,
    }, new ReportView(ReportCharts.Heatmap) {
        Sample = SampleRule.Dim,
        Unit = "egg",
        Fill = false,
        Heat = new HeatView("ship", "value", HeatScale.Relative),
        Labels = new Dictionary<string, string> { ["ship"] = "Ship" },
        Palette = "sunrise",
        Color = "#6366f1",
        Unfilled = "#000",
        LabelColors = new Dictionary<string, string> { ["henerprise"] = "#abcdef" },
        Options = new Dictionary<string, string> { ["mode"] = "compact" },
    }) { Description = "weekly" };

    private const string GreaterThan = "\\" + "u003E";
    private const string LegacyShapeJson =
        @"{""version"":1,""id"":""r1"",""title"":""Drops by ship"",""query"":{""source"":""drops"",""groupBy"":[{""dimension"":""ship"",""bucket"":""month"",""fillGaps"":false}],""measures"":[{""key"":""value"",""aggregation"":""sum""}],""filters"":{""and"":[{""topLevel"":""at"",""op"":""within"",""val"":""30d""}],""or"":[[{""topLevel"":""ship"",""op"":""="",""val"":""henerprise""}]]},""normalize"":{""kind"":""by"",""measure"":""airtime""},""sort"":[{""key"":""value"",""descending"":true}],""having"":[{""measureKey"":""value"",""op"":""" + GreaterThan + @""",""threshold"":2}],""limit"":5,""minSample"":3},""view"":{""chart"":""heatmap"",""sample"":""dim"",""unit"":""egg"",""fill"":false,""heat"":{""colorBy"":""ship"",""secondaryBy"":""value"",""scale"":""relative""},""labels"":{""ship"":""Ship""},""palette"":""sunrise"",""color"":""#6366f1"",""labelColors"":{""henerprise"":""#abcdef""},""unfilled"":""#000"",""options"":{""mode"":""compact""}},""description"":""weekly""}";

    [Fact]
    public void LegacyReportDefinitionJson_RoundTripsThroughSourceGeneratedCodec() =>
        Assert.Equal(LegacyShapeJson, ReportCodec.Serialize(ReportCodec.Deserialize(LegacyShapeJson)));

    [Fact]
    public void LegacyReportDefinitionJson_DeserializesToExpectedValues() {
        var back = ReportCodec.Deserialize(LegacyShapeJson);
        Assert.Equal(Sample().Query.GroupBy, back.Query.GroupBy);
        Assert.Equal(Sample().Query.Measures, back.Query.Measures);
        Assert.Equal(Normalize.By("airtime"), back.Query.Normalize);
        Assert.Equal(Sample().Query.Having, back.Query.Having);
        Assert.Equal(Sample().Query.Sort, back.Query.Sort);
        Assert.Equal(5, back.Query.Limit);
        Assert.Equal(SampleRule.Dim, back.View.Sample);
        Assert.Equal(HeatScale.Relative, back.View.Heat?.Scale);
        Assert.Equal("#abcdef", back.View.LabelColors["henerprise"]);
        Assert.Equal("compact", back.View.Options["mode"]);
    }
}
