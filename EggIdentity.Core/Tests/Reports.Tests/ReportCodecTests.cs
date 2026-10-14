using System.Text.Json;
using System.Text.Json.Nodes;
using EggIdentity.Filters;

namespace EggIdentity.Reports.Tests;

public class ReportCodecTests {
    private static ReportDefinition Sample() => new(1, "r1", "Drops by ship", new ReportQuery("drops") {
        GroupBy = [new GroupKey("ship"), new GroupKey("at", TimeBucket.Week, FillGaps: false)],
        Measures = [new MeasureRequest("value", Aggregation.Avg)],
        Filters = new FilterSet([new("at", FilterOps.Within, "30d")], [[new("ship", "=", "henerprise")]]),
        Normalize = Normalize.By("airtime"),
        Sort = [new SortKey("value", Descending: true)],
        Having = [new HavingClause("value", FilterOps.Gt, 2)],
        Limit = 5,
        MinSample = 3,
    }, new ReportView(ReportCharts.Heatmap) {
        Sample = SampleRule.Dim,
        Color = "#6366f1",
        Unfilled = "#000",
        LabelColors = new Dictionary<string, string> { ["henerprise"] = "#abcdef" },
        Options = new Dictionary<string, string> { ["mode"] = "compact" },
    }) { Description = "weekly" };

    [Fact]
    public void RoundTrip_PreservesTheDefinition() {
        var back = ReportCodec.Deserialize(ReportCodec.Serialize(Sample()));
        Assert.Equal(1, back.Version);
        Assert.Equal("weekly", back.Description);
        Assert.Equal(Sample().Query.GroupBy, back.Query.GroupBy);
        Assert.Equal(Sample().Query.Measures, back.Query.Measures);
        Assert.Equal(Normalize.By("airtime"), back.Query.Normalize);
        Assert.Equal(Sample().Query.Having, back.Query.Having);
        Assert.Equal(Sample().Query.Sort, back.Query.Sort);
        Assert.Equal(5, back.Query.Limit);
        Assert.Equal(FilterCodec.Hash("x", Sample().Query.Filters), FilterCodec.Hash("x", back.Query.Filters));
        Assert.Equal(SampleRule.Dim, back.View.Sample);
        Assert.Equal("#000", back.View.Unfilled);
        Assert.Equal("#abcdef", back.View.LabelColors["henerprise"]);
        Assert.Equal("compact", back.View.Options["mode"]);
    }

    [Fact]
    public void Filters_KeepTheFilterCodecWireShape() {
        var json = JsonNode.Parse(ReportCodec.Serialize(Sample()))!;
        var condition = json["query"]!["filters"]!["and"]![0]!;
        Assert.Equal("at", condition["topLevel"]!.GetValue<string>());
        Assert.Equal("within", condition["op"]!.GetValue<string>());
        Assert.Equal("30d", condition["val"]!.GetValue<string>());
    }

    [Fact]
    public void InvalidColors_AreDropped() {
        var json = JsonNode.Parse(ReportCodec.Serialize(Sample()))!.AsObject();
        var view = json["view"]!.AsObject();
        view["color"] = "red";
        view["unfilled"] = "#12345";
        view["labelColors"] = new JsonObject { ["a"] = "#fff", ["b"] = "url(x)", ["c"] = "#ggg" };
        var back = ReportCodec.Deserialize(json.ToJsonString());
        Assert.Null(back.View.Color);
        Assert.Null(back.View.Unfilled);
        Assert.Equal("#fff", Assert.Single(back.View.LabelColors).Value);
    }

    [Fact]
    public void MissingVersion_RunsTheVersionZeroStep() {
        var legacy = new JsonObject {
            ["id"] = "old",
            ["title"] = "Legacy",
            ["source"] = "drops",
            ["chart"] = "pie",
            ["gridX"] = 2,
        };
        var upgrades = new ReportUpgrades().Add(0, flat => new LegacySplit(
            new JsonObject {
                ["id"] = flat["id"]!.GetValue<string>(),
                ["title"] = flat["title"]!.GetValue<string>(),
                ["query"] = new JsonObject { ["source"] = flat["source"]!.GetValue<string>() },
                ["view"] = new JsonObject { ["chart"] = flat["chart"]!.GetValue<string>() },
            },
            new ReportPlacement(flat["id"]!.GetValue<string>(), flat["gridX"]!.GetValue<int>(), 0, 1, 1, 0)));
        var (definition, placement) = ReportCodec.Upgrade(legacy.ToJsonString(), upgrades);
        Assert.Equal(ReportCodec.CurrentVersion, definition.Version);
        Assert.Equal("drops", definition.Query.Source);
        Assert.Equal(ReportCharts.Pie, definition.View.Chart);
        Assert.Empty(definition.Query.GroupBy);
        Assert.Equal(2, placement?.X);
    }

    [Fact]
    public void MissingStep_Throws() =>
        Assert.Throws<JsonException>(() => ReportCodec.Upgrade("""{"id":"x"}""", new ReportUpgrades()));

    [Fact]
    public void FutureVersion_Throws() =>
        Assert.Throws<JsonException>(() => ReportCodec.Deserialize("""{"version":99,"id":"x","title":"t","query":{"source":"s"},"view":{"chart":"bar"}}"""));

    [Fact]
    public void Placement_RoundTrips() {
        var placement = new ReportPlacement("r1", 1, 2, 3, 4, 5) { Group = "main" };
        Assert.Equal(placement, ReportCodec.DeserializePlacement(ReportCodec.SerializePlacement(placement)));
    }
}
