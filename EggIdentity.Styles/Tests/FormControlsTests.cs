using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class FormControlsTests {
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
        Applies = Components.FormControls.Applies,
    });

    [Fact]
    public void FormInputFormSelectFormCheck_ProducesAllThreeBaseSelectors() {
        var css = BuildFramework().Process("form-input form-select form-check");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".form-input{", flat);
        Assert.Contains(".form-select{", flat);
        Assert.Contains(".form-check{", flat);
    }

    [Fact]
    public void FormInputWithFocus_ProducesFocusSelectorWithAccent2BorderColor() {
        var css = BuildFramework().Process("form-input");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".form-input:focus{", flat);
        Assert.Contains("#5aa9e6", css);
    }
}
