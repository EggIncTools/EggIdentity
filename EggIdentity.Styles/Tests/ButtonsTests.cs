using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class ButtonsTests {
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
        Applies = Components.Buttons.Applies,
    });

    [Fact]
    public void BtnPrimary_ProducesBaseDisabledAndHoverSelectors() {
        var css = BuildFramework().Process("btn-primary");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".btn-primary{", flat);
        Assert.Contains(".btn-primary:disabled{", flat);
        Assert.Contains(".btn-primary:hover:not(:disabled){", flat);
        Assert.Contains("cursor:not-allowed", flat);
        Assert.Contains("opacity:40%", flat);
    }

    [Fact]
    public void IconBtn_AloneAndWithActive_ProducesSelectorsWithAccentColor() {
        var css = BuildFramework().Process("icon-btn icon-btn active");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".icon-btn{", flat);
        Assert.Contains(".icon-btn.active{", flat);
        Assert.Contains("#ef7559", css);
    }

    [Fact]
    public void BtnMiniAndBtnDanger_ProducesBothSelectors() {
        var css = BuildFramework().Process("btn-mini btn-danger");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".btn-mini{", flat);
        Assert.Contains(".btn-danger.btn-danger{", flat);
    }
}
