using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class SegmentedTogglesTests {
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
        Applies = Components.SegmentedToggles.Applies,
    });

    [Fact]
    public void SegmentedAndSegmentedOpt_ProducesBothSelectors() {
        var css = BuildFramework().Process("segmented segmented-opt");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".segmented{", flat);
        Assert.Contains(".segmented-opt{", flat);
    }

    [Fact]
    public void SegmentedOpt_ProducesFirstChildDividerCollapse() {
        var css = BuildFramework().Process("segmented segmented-opt");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".segmented-opt:first-child{", flat);
        Assert.Contains("border-left-width:0px", flat);
        Assert.Contains("border-left-width:1px", flat);
    }

    [Fact]
    public void SegmentedOpt_AloneAndWithActive_ProducesSelectorsWithAccentBackground() {
        var css = BuildFramework().Process("segmented-opt segmented-opt active");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".segmented-opt{", flat);
        Assert.Contains(".segmented-opt.active{", flat);
        Assert.Contains("#ef7559", css);
    }
}
