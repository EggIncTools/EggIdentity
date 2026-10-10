namespace EggIdentity.Icons.Tests;

public class IconPackTests {
    [Fact]
    public void Names_ContainsVendoredLucideAndBrandIcons() {
        Assert.True(IconPack.Names.Count > 100);
        Assert.Contains("x", IconPack.Names);
        Assert.Contains("brand-github", IconPack.Names);
    }

    [Fact]
    public void TryGet_ResolvesAlias() {
        Assert.True(IconPack.TryGet("close", out var aliased));
        Assert.True(IconPack.TryGet("x", out var direct));
        Assert.Equal(direct, aliased);
    }

    [Fact]
    public void TryGet_UnknownName_ReturnsFalseAndNull() {
        Assert.False(IconPack.TryGet("no-such-icon", out _));
        Assert.Null(IconPack.Get("no-such-icon"));
    }

    [Theory]
    [InlineData("x")]
    [InlineData("brand-github")]
    public void Get_NormalizesRootMarkup(string name) {
        var svg = IconPack.Get(name)!;
        Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("width=\"100%\"", svg, StringComparison.Ordinal);
        Assert.Contains("height=\"100%\"", svg, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", svg, StringComparison.Ordinal);
        Assert.Contains("focusable=\"false\"", svg, StringComparison.Ordinal);
        Assert.Contains("viewBox=\"0 0 24 24\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<title", svg, StringComparison.Ordinal);
        Assert.DoesNotContain(" class=", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Get_BrandIcon_AddsStrokeNoneAndCurrentColorFill() {
        var svg = IconPack.Get("brand-github")!;
        Assert.Contains("stroke=\"none\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"currentColor\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Get_LucideIcon_KeepsStrokeAttributes() {
        var svg = IconPack.Get("x")!;
        Assert.Contains("stroke=\"currentColor\"", svg, StringComparison.Ordinal);
        Assert.Contains("stroke-width=\"2\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"none\"", svg, StringComparison.Ordinal);
    }
}
