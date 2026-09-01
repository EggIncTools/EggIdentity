using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class TooltipsTests {
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
            .Add("--color-border", "#3a3a44")
            .Add("--color-warn", "#e0b23a");

    private static CssFramework BuildFramework() => new(new CssFrameworkSettings {
        Theme = BuildTheme(),
        IncludePreflight = false,
        Applies = Components.Tooltips.Applies,
    });

    [Fact]
    public void TooltipFloating_DefaultsToOriginalBorderAndBgViaFallback() {
        var css = BuildFramework().Process("tooltip-floating");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains("border-color:var(--tooltip-border,var(--color-border))", flat);
        Assert.Contains("background-color:var(--tooltip-bg,rgba(33,37,41,.95))", flat);
    }

    [Fact]
    public void TooltipFixed_UsesPositionVars() {
        var css = BuildFramework().Process("tooltip-floating tooltip-fixed");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".tooltip-fixed{", flat);
        Assert.Contains("left:var(--tt-left,50%)", flat);
        Assert.Contains("top:var(--tt-top,50%)", flat);
    }

    [Fact]
    public void TooltipToggle_HiddenByDefault() {
        var css = BuildFramework().Process("tooltip-toggle");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".tooltip-toggle{", flat);
        Assert.Contains("opacity:0%", flat);
    }

    [Fact]
    public void TooltipToggleShow_RevealsAtFullOpacity() {
        var css = BuildFramework().Process("tooltip-toggle show");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".tooltip-toggle.show{", flat);
        Assert.Contains("opacity:100%", flat);
    }

    [Fact]
    public void TooltipBelow_FlipsArrowToBottomBorder() {
        var css = BuildFramework().Process("tooltip-floating tooltip-below");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".tooltip-floating.tooltip-below::before{", flat);
        Assert.Contains("border-bottom-color:var(--tooltip-border,var(--color-border))", flat);
    }

    [Fact]
    public void TooltipErr_OverridesTokensAndGlow() {
        var css = BuildFramework().Process("tooltip-floating tooltip-err");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".tooltip-floating.tooltip-err{", flat);
        Assert.Contains("--tooltip-border:var(--color-err)", flat);
    }

    [Fact]
    public void AllCoreSelectors_ProcessWithoutError() {
        var css = BuildFramework().Process(
            "tooltip-floating tooltip-anchored tooltip-fixed tooltip-host tooltip-toggle show tooltip-below tooltip-err");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".tooltip-floating{", flat);
        Assert.Contains(".tooltip-anchored{", flat);
        Assert.Contains(".tooltip-fixed{", flat);
    }
}
