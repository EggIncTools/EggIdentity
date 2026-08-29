using System.Collections.Immutable;
using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class Phase0SpikeTests {
    private static Theme BuildEggIncognitoTheme() =>
        Theme.CreateWithDefaults([])
            .Add("--color-bg", "#1b1b1f")
            .Add("--color-panel0", "#202027")
            .Add("--color-panel", "#25252b")
            .Add("--color-panel2", "#2e2e36")
            .Add("--color-fg", "#e7e7ea")
            .Add("--color-muted", "#9a9aa5")
            .Add("--color-accent", "#ef7559")
            .Add("--color-info", "#5aa9e6")
            .Add("--color-ok", "#5ec27e")
            .Add("--color-err", "#e0685f")
            .Add("--color-border", "#3a3a44")
            .Add("--spacing-nav", "48px")
            .Add("--radius-pill", "999px");

    private static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".btn-primary", "inline-block bg-accent text-bg border-0 rounded-md px-[1.4rem] py-2 font-semibold text-sm cursor-pointer no-underline" },
        { ".btn-primary:disabled", "cursor-not-allowed opacity-40" },
        { ".btn-primary:hover:not(:disabled)", "shadow-[0_3px_12px_-3px_rgba(239,117,89,.45)] brightness-[1.07]" },
    }.ToImmutableDictionary();

    private static CssFramework BuildFramework() => new(new CssFrameworkSettings {
        Theme = BuildEggIncognitoTheme(),
        IncludePreflight = false,
        Applies = Applies,
    });

    [Fact]
    public void ThemeFunction_InsideArbitraryValue_ResolvesToRawCssValue() {
        var css = BuildFramework().Process("top-[calc(theme(spacing.nav)+10px)]");

        Assert.Contains("calc(48px + 10px)", css);
    }

    [Fact]
    public void OpacityModifier_OnCustomColor_EmitsPercentageAlpha() {
        var css = BuildFramework().Process("bg-ok/15");

        Assert.Contains(".bg-ok\\/15", css);
        Assert.Contains("#5ec27e", css);
        Assert.Contains("15%", css);
    }

    [Fact]
    public void ResponsiveBreakpointPair_EmitsMediaQueries() {
        var css = BuildFramework().Process("px-4 sm:px-8");

        Assert.Contains(".px-4", css);
        Assert.Contains("@media", css);
        Assert.Contains(".sm\\:px-8", css);
    }

    [Fact]
    public void ArbitraryPropertyUtility_EmitsRawDeclaration() {
        var css = BuildFramework().Process("[word-break:break-word]");

        Assert.Contains("word-break:break-word", css.Replace(" ", ""));
    }

    [Fact]
    public void MultiStateComponentClass_AppliesBaseDisabledAndHoverNotDisabled() {
        var css = BuildFramework().Process("btn-primary");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".btn-primary{", flat);
        Assert.Contains(".btn-primary:disabled{", flat);
        Assert.Contains(".btn-primary:hover:not(:disabled){", flat);
        Assert.Contains("cursor:not-allowed", flat);
        Assert.Contains("opacity:40%", flat);
    }
}
