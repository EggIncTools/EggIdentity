using EggIdentity.Styles.Theming;

namespace EggIdentity.Styles.Tests;

public class ThemeCssParserTests {
    private static readonly ThemeCssCatalog Catalog = new([
        new ThemeCatalogEntry("panel", ".panel", ThemePropertyGroup.Surface),
        new ThemeCatalogEntry("button", ".btn-mini", ThemePropertyGroup.Full),
        new ThemeCatalogEntry("table-row", ".data-table tbody tr", ThemePropertyGroup.Surface),
        new ThemeCatalogEntry("table-header", ".data-table th", ThemePropertyGroup.Text),
        new ThemeCatalogEntry("scrollbar-thumb", "::-webkit-scrollbar-thumb", ThemePropertyGroup.ColorOnly),
    ]);

    private static readonly ThemeTokenRegistry Tokens = new ThemeTokenRegistry().Register("accent");

    [Theory]
    [InlineData("@import url(evil.css);")]
    [InlineData("@media (min-width: 1px) { panel { color: red } }")]
    [InlineData("panel { background-image: url(https://x/y.png) }")]
    [InlineData("panel { width: expression(alert(1)) }")]
    [InlineData("panel { -moz-binding: url(x) }")]
    [InlineData("panel { behavior: url(x.htc) }")]
    [InlineData("panel { color: red }</style><script>alert(1)</script>")]
    [InlineData("panel { content: attr(data-secret) }")]
    [InlineData("panel { position: fixed }")]
    [InlineData("panel { z-index: 99999 }")]
    [InlineData("panel { pointer-events: none }")]
    [InlineData("panel { background-image: linear-gradient(url(x), red) }")]
    [InlineData("panel { color: red !important }")]
    [InlineData("panel { /* comment")]
    [InlineData("panel { color: red")]
    [InlineData("panel { background-image: u\\72 l(x) }")]
    [InlineData("panel { col\\6fr: red }")]
    [InlineData("panel { colo\u00A0r: red }")]
    [InlineData("pa\u200Bnel { color: red }")]
    [InlineData("panel { color: red, }")]
    [InlineData("login { color: red }")]
    [InlineData("auth-name { color: red }")]
    [InlineData("body { color: red }")]
    [InlineData("* { color: red }")]
    [InlineData("panel:hover { color: red }")]
    [InlineData(".panel { color: red }")]
    [InlineData("panel { box-shadow: 0 0 2px red, 0 0 2px blue, 0 0 2px green }")]
    [InlineData("scrollbar-thumb { box-shadow: 0 0 4px red }")]
    [InlineData("panel { color: var(--color-red-500) }")]
    [InlineData("panel { color: var(--egi-glow) }")]
    [InlineData("panel { transition-property: all }")]
    [InlineData("panel { opacity: red }")]
    [InlineData("panel { font-weight: url(x) }")]
    [InlineData("panel { background-image: image-set(url(x) 1x) }")]
    [InlineData("panel { color: env(secret) }")]
    [InlineData("panel { color: element(#x) }")]
    public void HostileInput_IsRejectedWhole(string input) {
        var result = Parse(input);
        Assert.False(result.Ok);
        Assert.Empty(result.Rules);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void OversizedSource_IsRejected() {
        string input = "panel { color: red }" + new string(' ', ThemeModel.MaxCssSourceBytes);
        var result = Parse(input);
        Assert.False(result.Ok);
    }

    [Fact]
    public void UppercaseInput_IsCaseFolded() {
        var result = Parse("PANEL { COLOR: RED }");
        Assert.True(result.Ok);
        Assert.Equal(".panel", result.Rules[0].Entry.Selector);
        Assert.Equal("color", result.Rules[0].Declarations[0].Property);
        Assert.Equal("red", Assert.IsType<CssKeyword>(result.Rules[0].Declarations[0].Groups[0][0]).Text);
    }

    [Fact]
    public void SettableVar_IsCanonicalized() {
        var result = Parse("panel { color: var(--color-accent) }");
        Assert.True(result.Ok);
        var func = Assert.IsType<CssFunc>(result.Rules[0].Declarations[0].Groups[0][0]);
        Assert.Equal("var", func.Name);
        Assert.Equal("--color-accent", Assert.IsType<CssKeyword>(func.Args[0][0]).Text);
    }

    [Fact]
    public void CommentsAreDropped_NeverEmitted() {
        var result = Parse("panel { /* a comment */ color: red }");
        Assert.True(result.Ok);
        Assert.Single(result.Rules[0].Declarations);
        Assert.Equal("color", result.Rules[0].Declarations[0].Property);
    }

    [Fact]
    public void TwoShadows_Parse_AndThreeDoNot() {
        Assert.True(Parse("panel { box-shadow: 0 0 2px red, inset 0 1px 4px 2px #001122 }").Ok);
        Assert.False(Parse("panel { box-shadow: 0 0 2px red, 0 0 2px blue, 0 0 2px green }").Ok);
    }

    [Fact]
    public void Gradients_ParseWithinTheGrammar() {
        Assert.True(Parse(
            "panel { background-image: linear-gradient(135deg, var(--color-panel) 0%, var(--color-accent) 100%) }").Ok);
        Assert.True(Parse(
            "panel { background-image: radial-gradient(circle, #112233, transparent) }").Ok);
        Assert.False(Parse(
            "panel { background-image: conic-gradient(red, blue) }").Ok);
    }

    [Fact]
    public void ColorMix_OnlyInOklab() {
        Assert.True(Parse(
            "panel { color: color-mix(in oklab, var(--color-accent) 40%, transparent) }").Ok);
        Assert.False(Parse(
            "panel { color: color-mix(in srgb, red 40%, blue) }").Ok);
    }

    [Fact]
    public void RetiredNavSurfaces_AreUnknown() {
        var nav = Parse("nav { color: red }");
        Assert.False(nav.Ok);
        Assert.Contains(nav.Errors, e => e.Message.Contains("unknown surface", StringComparison.Ordinal));
        Assert.False(Parse("nav-item { color: red }").Ok);
    }

    [Fact]
    public void EveryCatalogGroup_EnforcesItsPropertyFloor() {
        Assert.False(Parse("scrollbar-thumb { font-weight: 700 }").Ok);
        Assert.False(Parse("table-row { font-weight: 700 }").Ok);
        Assert.True(Parse("table-header { font-weight: 700 }").Ok);
        Assert.False(Parse("table-header { opacity: 0.5 }").Ok);
        Assert.True(Parse("button { opacity: 0.5 }").Ok);
    }

    [Fact]
    public void OutOfRangeOpacity_IsClampedToFloor() {
        var result = Parse("button { opacity: 0 }");
        Assert.True(result.Ok, string.Join("; ", result.Errors.Select(e => e.Message)));
        var value = Assert.IsType<CssNumber>(result.Rules[0].Declarations[0].Groups[0][0]);
        Assert.Equal(0.35, value.Value);
    }

    [Fact]
    public void OutOfRangeBorderRadius_IsClampedToCeiling() {
        var result = Parse("button { border-radius: 9999px }");
        Assert.True(result.Ok, string.Join("; ", result.Errors.Select(e => e.Message)));
        var value = Assert.IsType<CssNumber>(result.Rules[0].Declarations[0].Groups[0][0]);
        Assert.Equal(32, value.Value);
    }

    [Fact]
    public void OutOfRangeBorderWidth_IsClampedToCeiling() {
        var result = Parse("button { border-width: 100px }");
        Assert.True(result.Ok, string.Join("; ", result.Errors.Select(e => e.Message)));
        var value = Assert.IsType<CssNumber>(result.Rules[0].Declarations[0].Groups[0][0]);
        Assert.Equal(4, value.Value);
    }

    [Fact]
    public void OutOfRangeRgbChannel_IsClampedToCeiling() {
        var result = Parse("button { color: rgb(999, 0, 0) }");
        Assert.True(result.Ok, string.Join("; ", result.Errors.Select(e => e.Message)));
        var func = Assert.IsType<CssFunc>(result.Rules[0].Declarations[0].Groups[0][0]);
        var channel = Assert.IsType<CssNumber>(func.Args[0][0]);
        Assert.Equal(255, channel.Value);
    }

    [Fact]
    public void OutOfRangeFontWeight_IsClampedToCeiling() {
        var result = Parse("button { font-weight: 9000 }");
        Assert.True(result.Ok, string.Join("; ", result.Errors.Select(e => e.Message)));
        var value = Assert.IsType<CssNumber>(result.Rules[0].Declarations[0].Groups[0][0]);
        Assert.Equal(900, value.Value);
    }

    [Fact]
    public void OutOfRangeLetterSpacing_IsClampedToCeiling() {
        var result = Parse("button { letter-spacing: 5em }");
        Assert.True(result.Ok, string.Join("; ", result.Errors.Select(e => e.Message)));
        var value = Assert.IsType<CssNumber>(result.Rules[0].Declarations[0].Groups[0][0]);
        Assert.Equal(0.2, value.Value);
    }

    [Fact]
    public void OutOfRangeTransitionDuration_IsClampedToCeiling() {
        var result = Parse("button { transition-duration: 90s }");
        Assert.True(result.Ok, string.Join("; ", result.Errors.Select(e => e.Message)));
        var value = Assert.IsType<CssNumber>(result.Rules[0].Declarations[0].Groups[0][0]);
        Assert.Equal(1000, value.Value);
    }

    private static CssParseResult Parse(string input) =>
        ThemeCssParser.Parse(input, Catalog, Tokens, ThemeModel.MaxCssSourceBytes);
}
