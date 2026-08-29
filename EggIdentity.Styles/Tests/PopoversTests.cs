using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class PopoversTests {
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
        Applies = Components.Popovers.Applies,
    });

    [Fact]
    public void Popover_ProducesBaseSelectorWithZeroOpacityAndArbitraryDeclarations() {
        var css = BuildFramework().Process("popover");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".popover{", flat);
        Assert.Contains("opacity:0%", flat);
        Assert.Contains("calc(100%+8px)", flat);
        Assert.Contains("transform-origin: top right", css);
        Assert.Contains("transition: opacity .14s ease,transform .14s ease", css);
        Assert.Contains("0 6px 18px rgba(0,0,0,.4)", css);
        Assert.Contains("transform: scale(.96) translateY(-6px)", css);
    }

    [Fact]
    public void PopoverOpen_ProducesCompoundSelectorWithFullOpacity() {
        var css = BuildFramework().Process("popover open");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".popover.open{", flat);
        Assert.Contains("opacity:100%", flat);
        Assert.Contains("transform: scale(1) translateY(0)", css);
    }

    [Fact]
    public void PopoverWrapSmAndLg_ProduceAllSelectors() {
        var css = BuildFramework().Process("popover-wrap popover popover-sm popover-lg");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".popover-wrap{", flat);
        Assert.Contains(".popover.popover-sm{", flat);
        Assert.Contains(".popover.popover-lg{", flat);
    }
}
