using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class FloatingBubblesTests {
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
        Applies = Components.FloatingBubbles.Applies,
    });

    [Fact]
    public void FabBubble_ProducesCallerOverridableStackingOffset() {
        var css = BuildFramework().Process("fab-bubble");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains("right:calc(var(--spacing)*4)", flat);
        Assert.Contains("bottom:var(--fab-offset,1rem)", flat);
    }

    [Fact]
    public void FabBubble_ProducesBaseAndHoverSelectorsWithBorderColors() {
        var css = BuildFramework().Process("fab-bubble");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".fab-bubble{", flat);
        Assert.Contains(".fab-bubble:hover{", flat);
        Assert.Contains("#3a3a44", css);
        Assert.Contains("#5aa9e6", css);
    }
}
