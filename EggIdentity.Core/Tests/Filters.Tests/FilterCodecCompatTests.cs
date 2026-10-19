namespace EggIdentity.Filters.Tests;

public class FilterCodecCompatTests {
    private const string Quote = "\\" + "u0022";
    private const string LegacyShapeJson =
        "{\"and\":[{\"topLevel\":\"rarity\",\"op\":\"in\",\"val\":\"[" + Quote + "legendary" + Quote + "]\"}],\"or\":[[{\"topLevel\":\"launched_at\",\"op\":\"within\",\"val\":\"30d\"}]]}";

    [Fact]
    public void LegacyFilterSetJson_RoundTripsThroughSourceGeneratedCodec() =>
        Assert.Equal(LegacyShapeJson, FilterCodec.Serialize(FilterCodec.Deserialize(LegacyShapeJson)));

    [Fact]
    public void LegacyFilterSetJson_DeserializesToExpectedValues() {
        var set = FilterCodec.Deserialize(LegacyShapeJson);
        Assert.Equal(new FilterCondition("rarity", "in", "[\"legendary\"]"), set.And[0]);
        Assert.Equal(new FilterCondition("launched_at", "within", "30d"), Assert.Single(set.Or[0]));
    }
}
