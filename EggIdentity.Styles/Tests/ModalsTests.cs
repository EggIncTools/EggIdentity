using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class ModalsTests {
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
        Applies = Components.Modals.Applies,
    });

    [Fact]
    public void AllModalSelectors_ProcessAllFiveClasses() {
        var css = BuildFramework().Process("modal-backdrop modal-card modal-head modal-title modal-body");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".modal-backdrop{", flat);
        Assert.Contains(".modal-card{", flat);
        Assert.Contains(".modal-head{", flat);
        Assert.Contains(".modal-title{", flat);
        Assert.Contains(".modal-body{", flat);
    }

    [Fact]
    public void ModalCard_ProducedsPanelRoleHexColor() {
        var css = BuildFramework().Process("modal-card");

        Assert.Contains("#25252b", css);
    }
}
