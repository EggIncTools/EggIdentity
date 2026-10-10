namespace EggIdentity.Fleet.Tests;

public class StackLookupTests {
    private static readonly PortainerStack Apps = PortainerFakes.Parse(PortainerFakes.GitStack);
    private static readonly PortainerStack Db = PortainerFakes.Parse(PortainerFakes.WebEditorStack());

    [Fact]
    public void Find_MatchesNameIgnoringCase() {
        var (stack, refusal) = StackLookup.Find([Apps, Db], "EGG-APPS");

        Assert.Same(Apps, stack);
        Assert.Null(refusal);
    }

    [Fact]
    public void Find_Missing_NamesTheStack() {
        var (stack, refusal) = StackLookup.Find([Db], "egg-apps");

        Assert.Null(stack);
        Assert.Contains("no stack named \"egg-apps\"", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Find_Duplicate_RefusesAndListsBothIds() {
        var twin = PortainerFakes.Parse(PortainerFakes.WebEditorStack(id: 71, name: "egg-apps"));

        var (stack, refusal) = StackLookup.Find([Apps, twin], "egg-apps");

        Assert.Null(stack);
        Assert.Contains("56", refusal, StringComparison.Ordinal);
        Assert.Contains("71", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Find_BlankName_Refuses() =>
        Assert.NotNull(StackLookup.Find([Apps], " ").Refusal);

    [Fact]
    public void Describe_ArmedGitStack_IsReady() {
        var info = StackLookup.Describe(Apps);

        Assert.True(info.Ready);
        Assert.True(info.GitBacked);
        Assert.Equal("refs/heads/main", info.ReferenceName);
        Assert.Equal(56, info.StackId);
    }

    [Fact]
    public void Describe_GitStackWithoutWebhook_NamesTheMissingPiece() {
        var bare = PortainerFakes.Parse(PortainerFakes.GitStack.Replace(PortainerFakes.ArmedAutoUpdate, "null", StringComparison.Ordinal));

        Assert.Contains("GitOps webhook", StackLookup.Describe(bare).Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_WebEditorStack_IsReady() =>
        Assert.True(StackLookup.Describe(Db).Ready);
}
