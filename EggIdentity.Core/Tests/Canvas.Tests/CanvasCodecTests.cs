using System.Text.Json;
using System.Text.Json.Nodes;

namespace EggIdentity.Canvas.Tests;

public class CanvasCodecTests {
    private static CanvasDoc Doc() => new(6, 3, [
        new CanvasItem("a", "field", 1, 1, 2, 1, 0) { Payload = "{\"field\":\"ship\"}", Style = new ItemStyle { Color = "#abc", Align = TextAlign.End } },
        new CanvasItem("b", "spark", 3, 2, 4, 2, 1) { Rotation = 90 },
    ]);

    [Fact]
    public void RoundTripsWithPayload() {
        var back = CanvasCodec.Deserialize(CanvasCodec.Serialize(Doc()));

        Assert.Equal(Doc(), back);
    }

    [Fact]
    public void NullsAreOmittedAndEnumsAreCamel() {
        var json = JsonNode.Parse(CanvasCodec.Serialize(Doc()))!.AsObject();
        var items = json["items"]!.AsArray();

        Assert.False(items[1]!.AsObject().ContainsKey("style"));
        Assert.False(items[0]!.AsObject().ContainsKey("rotation"));
        Assert.Equal("end", items[0]!["style"]!["align"]!.GetValue<string>());
        Assert.Equal(1, json["version"]!.GetValue<int>());
    }

    [Fact]
    public void DeserializeCleansTheDoc() {
        var json = CanvasCodec.Serialize(new CanvasDoc(4, 2, [new CanvasItem("a", "text", 9, 9, 9, 9, 5) { Style = new ItemStyle { Border = "blue" } }]));
        var item = CanvasCodec.Deserialize(json).Items.Single();

        Assert.Equal((1, 1, 4, 2, 0), (item.X, item.Y, item.W, item.H, item.Z));
        Assert.Null(item.Style!.Border);
    }

    [Fact]
    public void NewerVersionThrows() {
        var json = JsonNode.Parse(CanvasCodec.Serialize(Doc()))!.AsObject();
        json["version"] = CanvasCodec.CurrentVersion + 1;

        Assert.Throws<JsonException>(() => CanvasCodec.Deserialize(json.ToJsonString()));
    }
}
