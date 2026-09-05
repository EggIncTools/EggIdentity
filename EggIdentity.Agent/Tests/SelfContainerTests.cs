using System.Text.Json;

namespace EggIdentity.Agent.Tests;

public class SelfContainerTests {
    private const string FullId = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void IsSelf_HostnameIsShortIdPrefix_True() =>
        Assert.True(SelfContainer.IsSelf(Container(FullId, "eggidentity-agent"), "0123456789ab", null));

    [Fact]
    public void IsSelf_HostnameIsUppercaseShortId_True() =>
        Assert.True(SelfContainer.IsSelf(Container(FullId, "eggidentity-agent"), "0123456789AB", null));

    [Fact]
    public void IsSelf_HostnameIsUnrelatedMachineName_False() =>
        Assert.False(SelfContainer.IsSelf(Container(FullId, "eggidentity-agent"), "DESKTOP-01", null));

    [Fact]
    public void IsSelf_ShortHostnameThatHappensToPrefix_False() =>
        Assert.False(SelfContainer.IsSelf(Container(FullId, "eggidentity-agent"), "0123", null));

    [Fact]
    public void IsSelf_EnvNameMatchesContainerName_True() =>
        Assert.True(SelfContainer.IsSelf(Container(FullId, "eggidentity-agent"), "web-1", "eggidentity-agent"));

    [Fact]
    public void IsSelf_EnvNameDiffers_False() =>
        Assert.False(SelfContainer.IsSelf(Container(FullId, "eggledger"), "web-1", "eggidentity-agent"));

    [Fact]
    public void IsSelf_NothingKnown_False() =>
        Assert.False(SelfContainer.IsSelf(Container(FullId, "eggledger"), null, null));

    private static ContainerInfo Container(string id, string name) {
        using var doc = JsonDocument.Parse("{}");
        var empty = doc.RootElement.Clone();
        return new ContainerInfo(id, name, "img", "sha256:img", [], [], new Dictionary<string, string>(), true, empty, empty, empty);
    }
}
