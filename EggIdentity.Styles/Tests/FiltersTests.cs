using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class FiltersTests {
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
        Applies = Components.Filters.Applies,
    });

    [Fact]
    public void FilterGlueOuter_UsesAccentColor() {
        var css = BuildFramework().Process("filter-glue filter-glue-outer");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".filter-glue-outer{", flat);
        Assert.Contains("color:var(--color-accent)", flat);
    }

    [Fact]
    public void FilterAddInner_UsesAccent2Color() {
        var css = BuildFramework().Process("filter-add-btn filter-add-inner");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".filter-add-inner{", flat);
        Assert.Contains("color:var(--color-accent2)", flat);
    }

    [Fact]
    public void FilterAddOuter_UsesDashedBorder() {
        var css = BuildFramework().Process("filter-add-btn filter-add-outer");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains("border-style:dashed", flat);
    }

    [Fact]
    public void FilterRemoveBtn_UsesErrColor() {
        var css = BuildFramework().Process("filter-remove-btn");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".filter-remove-btn{", flat);
        Assert.Contains("color:var(--color-err)", flat);
    }

    [Fact]
    public void AllCoreSelectors_ProcessWithoutError() {
        var css = BuildFramework().Process(
            "filter-panel filter-bucket filter-row filter-glue filter-glue-outer filter-glue-inner " +
            "filter-warn filter-add-btn filter-add-inner filter-add-outer filter-remove-btn");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".filter-panel{", flat);
        Assert.Contains(".filter-row{", flat);
        Assert.Contains(".filter-remove-btn{", flat);
    }
}
