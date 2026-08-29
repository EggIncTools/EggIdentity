using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class CompositionTests {
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

    [Fact]
    public void ComponentClasses_CompoundModifierSelectors_CompileWithBoostedSpecificity() {
        var framework = new CssFramework(new CssFrameworkSettings {
            Theme = BuildTheme(),
            IncludePreflight = false,
            Applies = ComponentClasses.All,
        });
        var css = framework.Process("badge badge-accent popover popover-sm btn-danger");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".badge.badge-accent{", flat);
        Assert.Contains(".popover.popover-sm{", flat);
        Assert.Contains(".btn-danger.btn-danger{", flat);
    }
}
