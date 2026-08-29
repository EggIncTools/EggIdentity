using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class PanelsTests {
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
        Applies = Components.Panels.Applies,
    });

    [Fact]
    public void Panel_ProducesSelectorsWithPanelBackground() {
        var css = BuildFramework().Process("panel");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".panel{", flat);
        Assert.Contains("#25252b", css);
    }

    [Fact]
    public void PanelHeadAndPanelTitle_ProducesBothSelectors() {
        var css = BuildFramework().Process("panel-head panel-title");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".panel-head{", flat);
        Assert.Contains(".panel-title{", flat);
    }
}
