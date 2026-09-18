namespace EggIdentity.DbClone.Tests;

public class LoadOrderTests {
    [Fact]
    public void Sort_PutsParentsBeforeChildren() {
        var order = LoadOrder.Sort(
            ["identities", "users", "sessions"],
            [new ForeignKey("identities", "users"), new ForeignKey("sessions", "users")]);

        Assert.Equal("users", order[0]);
        Assert.Equal(3, order.Count);
    }

    [Fact]
    public void Sort_IgnoresSelfReferencesAndUngovernedTables() {
        var order = LoadOrder.Sort(
            ["tree"],
            [new ForeignKey("tree", "tree"), new ForeignKey("tree", "outside")]);

        Assert.Equal(["tree"], order);
    }

    [Fact]
    public void Sort_NamesTablesInACycle() {
        var e = Assert.Throws<InvalidOperationException>(() => LoadOrder.Sort(
            ["a", "b", "c"],
            [new ForeignKey("a", "b"), new ForeignKey("b", "a")]));

        Assert.Contains("a", e.Message, StringComparison.Ordinal);
        Assert.Contains("b", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("c", e.Message.Split(':')[1], StringComparison.Ordinal);
    }
}
