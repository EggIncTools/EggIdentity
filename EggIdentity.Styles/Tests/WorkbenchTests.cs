using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class WorkbenchTests {
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
        Applies = Components.Workbench.Applies,
    });

    [Fact]
    public void WbCard_DeclaresSizeTokens() {
        var css = BuildFramework().Process("modal-card wb-card");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".modal-card.wb-card{", flat);
        Assert.Contains("width:var(--wb-card-w,92vw)", flat);
        Assert.Contains("height:var(--wb-card-h,88vh)", flat);
        Assert.Contains("max-width:var(--wb-card-max,80rem)", flat);
    }

    [Fact]
    public void WbCardWide_OverridesCardTokens() {
        var css = BuildFramework().Process("modal-card wb-card wb-card-wide");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains("--wb-card-w:94vw", flat);
        Assert.Contains("--wb-card-h:90vh", flat);
        Assert.Contains("--wb-card-max:92rem", flat);
    }

    [Fact]
    public void WbRail_UsesTokenWidthNotFixedUtility() {
        var css = BuildFramework().Process("wb-rail");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains("width:var(--wb-rail-w,18rem)", flat);
    }

    [Fact]
    public void WbEntrySelectedAndCompare_ProduceAccentBorderAndBackground() {
        var css = BuildFramework().Process("wb-entry selected compare");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".wb-entry.selected{", flat);
        Assert.Contains(".wb-entry.compare{", flat);
        Assert.Contains("#ef7559", css);
    }

    [Fact]
    public void AllCoreSelectors_ProcessWithoutError() {
        var css = BuildFramework().Process(
            "modal-card wb-card wb-body wb-main wb-notice wb-head-tools wb-rail wb-entry wb-entry-head " +
            "wb-entry-name wb-entry-meta wb-entry-foot wb-sec wb-sec-head wb-sec-tools wb-sec-body " +
            "wb-scroll wb-note");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".wb-body{", flat);
        Assert.Contains(".wb-sec-body{", flat);
        Assert.Contains(".wb-note{", flat);
    }
}
