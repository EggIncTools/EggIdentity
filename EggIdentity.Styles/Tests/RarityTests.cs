using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class RarityTests {
    private static Theme BuildTheme() =>
        Theme.CreateWithDefaults([])
            .Add("--color-bg", "#1b1b1f")
            .Add("--color-panel", "#25252b")
            .Add("--color-panel2", "#2e2e36")
            .Add("--color-fg", "#e7e7ea")
            .Add("--color-muted", "#9a9aa5")
            .Add("--color-accent", "#ef7559")
            .Add("--color-accent2", "#5aa9e6")
            .Add("--color-ok", "#5ec27e")
            .Add("--color-err", "#e0685f")
            .Add("--color-border", "#3a3a44");

    private static CssFramework BuildFramework() => new(new CssFrameworkSettings {
        Theme = BuildTheme(),
        IncludePreflight = false,
        Applies = Components.Rarity.Applies,
    });

    private static string Flat(string css) => css.Replace(" ", "").Replace("\n", "").Replace("\r", "");

    [Theory]
    [InlineData("bg-r-0", ".bg-r-0{", "background-color:var(--rarity-bg-0,#443e45)")]
    [InlineData("bg-r-1", ".bg-r-1{", "background-color:var(--rarity-bg-1,#6ab6ff)")]
    [InlineData("bg-r-1", ".bg-r-1{", "background-image:var(--rarity-grad-1,radial-gradient(#a8dfff,#8dd5ff,#3a9dfc))")]
    [InlineData("bg-r-2", ".bg-r-2{", "background-color:var(--rarity-bg-2,#c03fe2)")]
    [InlineData("bg-r-2", ".bg-r-2{", "background-image:var(--rarity-grad-2,radial-gradient(#ce81f7,#b958ed,#8819c2))")]
    [InlineData("bg-r-3", ".bg-r-3{", "background-color:var(--rarity-bg-3,#eeab42)")]
    [InlineData("bg-r-3", ".bg-r-3{", "background-image:var(--rarity-grad-3,radial-gradient(#fcdd6a,#ffdb58,#e09143))")]
    [InlineData("text-rarity-0", ".text-rarity-0{", "color:var(--rarity-fg-0,rgb(156163175))")]
    [InlineData("text-rarity-1", ".text-rarity-1{", "color:var(--rarity-fg-1,#6ab6ff)")]
    [InlineData("text-rarity-2", ".text-rarity-2{", "color:var(--rarity-fg-2,#c03fe2)")]
    [InlineData("text-rarity-3", ".text-rarity-3{", "color:var(--rarity-fg-3,#eeab42)")]
    public void RarityRule_EmitsLedgerDeclarationsBehindTokens(string classes, string selector, string declaration) {
        var flat = Flat(BuildFramework().Process(classes));

        Assert.Contains(selector, flat);
        Assert.Contains(declaration, flat);
    }

    [Fact]
    public void BgR3_GradientKeepsLedgerCommaSpacing() {
        var css = BuildFramework().Process("bg-r-3");

        Assert.Contains("radial-gradient(#fcdd6a, #ffdb58, #e09143)", css);
    }

    [Fact]
    public void BgR1AndBgR2_GradientsHaveNoCommaSpacing() {
        var css = BuildFramework().Process("bg-r-1 bg-r-2");

        Assert.Contains("radial-gradient(#a8dfff,#8dd5ff,#3a9dfc)", css);
        Assert.Contains("radial-gradient(#ce81f7,#b958ed,#8819c2)", css);
    }

    [Fact]
    public void TextRarity0_KeepsSpaceSeparatedRgb() {
        var css = BuildFramework().Process("text-rarity-0");

        Assert.Contains("rgb(156 163 175)", css);
    }

    [Fact]
    public void BgR0_HasNoBackgroundImage() {
        var flat = Flat(BuildFramework().Process("bg-r-0"));
        var ruleStart = flat.IndexOf(".bg-r-0{", StringComparison.Ordinal);
        var ruleEnd = flat.IndexOf('}', ruleStart);
        var rule = flat[ruleStart..ruleEnd];

        Assert.DoesNotContain("background-image", rule);
    }

    [Fact]
    public void AllRarityRules_EmitWithoutScanCandidates() {
        var flat = Flat(BuildFramework().Process("unrelated-token"));

        foreach (var selector in new[] {
            ".bg-r-0{", ".bg-r-1{", ".bg-r-2{", ".bg-r-3{",
            ".text-rarity-0{", ".text-rarity-1{", ".text-rarity-2{", ".text-rarity-3{",
        }) {
            Assert.Contains(selector, flat);
        }
    }
}
