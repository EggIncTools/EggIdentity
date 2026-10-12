namespace EggIdentity.Filters.Tests;

public class FilterCodecTests {
    private const string Stored =
        """{"and":[{"topLevel":"ship","op":"eq","val":"henerprise"},{"topLevel":"dubcap","op":"true","val":""}],"or":[[{"topLevel":"farm","op":"=","val":"1"}],[]]}""";

    [Fact]
    public void StoredShape_RoundTripsExactly() =>
        Assert.Equal(Stored, FilterCodec.Serialize(FilterCodec.Deserialize(Stored)));

    [Fact]
    public void Deserialize_ReadsEggLedgerWireNames() {
        var set = FilterCodec.Deserialize(Stored);
        Assert.Equal(new FilterCondition("ship", "eq", "henerprise"), set.And[0]);
        Assert.Equal(new FilterCondition("farm", "=", "1"), Assert.Single(set.Or[0]));
        Assert.Empty(set.Or[1]);
    }

    [Fact]
    public void Deserialize_ToleratesNullsAndNumbers() {
        var set = FilterCodec.Deserialize("""{"and":[{"topLevel":"level","op":">","val":5}],"or":[null]}""");
        Assert.Equal("5", set.And[0].Value);
        Assert.Empty(set.Or[0]);
    }

    [Fact]
    public void Serialize_WritesOneOrListPerRow() =>
        Assert.Equal(
            """{"and":[{"topLevel":"a","op":"=","val":"1"},{"topLevel":"b","op":"=","val":"2"}],"or":[[],[]]}""",
            FilterCodec.Serialize(new FilterSet([new("a", "=", "1"), new("b", "=", "2")], [])));

    [Fact]
    public void Deserialize_BlankIsEmpty() => Assert.True(FilterCodec.Deserialize(" ").IsEmpty);

    [Fact]
    public void Hash_PairsOrSiblingsWithTheirRow() =>
        Assert.Equal("missions#ship~=~3|ship~=~4;ultra~true~;",
            FilterCodec.Hash("missions", new FilterSet(
                [new("ship", "=", "3"), new("ultra", "true", "")],
                [[new("ship", "=", "4")], []])));

    [Fact]
    public void Catalog_RoundTrips() {
        var catalog = new FilterCatalog("proto",
            [new FilterFieldDef("platform", "Platform", FilterValueKind.Select, FilterOps.Equality) { Options = [new FilterOption("ios", "iOS")] }],
            new FilterSet([new("platform", "=", "ios")], []));

        var back = FilterCodec.DeserializeCatalog(FilterCodec.SerializeCatalog(catalog));

        Assert.NotNull(back);
        Assert.Equal("ios", back.Fields[0].Options[0].Value);
        Assert.Equal(catalog.Defaults.And, back.Defaults.And);
    }
}
