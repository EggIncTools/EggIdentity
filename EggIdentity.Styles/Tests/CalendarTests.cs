using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Styles.Tests;

public class CalendarTests {
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
        Applies = Components.Calendar.Applies,
    });

    [Fact]
    public void CalNow_UsesErrColor() {
        var css = BuildFramework().Process("cal-now");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".cal-now{", flat);
        Assert.Contains("background-color:var(--color-err)", flat);
    }

    [Fact]
    public void CalRangeTriggerHover_UsesAccent2Border() {
        var css = BuildFramework().Process("cal-range-trigger");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".cal-range-trigger:hover{", flat);
        Assert.Contains("border-color:var(--color-accent2)", flat);
    }

    [Fact]
    public void CalRowFixed_UsesRowHeightVarWithFallback() {
        var css = BuildFramework().Process("cal-row cal-row-fixed");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".cal-row.cal-row-fixed{", flat);
        Assert.Contains("var(--row-h", flat);
    }

    [Fact]
    public void CalLaneGroupSibling_HasMarginTop() {
        var css = BuildFramework().Process("cal-lane-group");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".cal-lane-group+.cal-lane-group{", flat);
        Assert.Contains("margin-top:calc(var(--spacing)*2)", flat);
    }

    [Fact]
    public void CalRow_IsFlexColumn() {
        var css = BuildFramework().Process("cal-row");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".cal-row{", flat);
        Assert.Contains("flex-direction:column", flat);
    }

    [Fact]
    public void CalLaneGroupLastChild_GrowsToFillRow() {
        var css = BuildFramework().Process("cal-lane-group");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".cal-lane-group:last-child{", flat);
        Assert.Contains("flex:1", flat);
    }

    [Fact]
    public void CalPeriodContext_UsesReducedOpacity() {
        var css = BuildFramework().Process("cal-period-context");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".cal-period-context{", flat);
        Assert.Contains("opacity:0.55", flat);
    }

    [Fact]
    public void CalCellLabel_SitsAboveLaneContent() {
        var css = BuildFramework().Process("cal-cell-label");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".cal-cell-label{", flat);
        Assert.Contains("z-index:5", flat);
    }

    [Fact]
    public void AllCoreSelectors_ProcessWithoutError() {
        var css = BuildFramework().Process(
            "cal-viewport cal-strip cal-period cal-period-context cal-canvas cal-row cal-row-fixed cal-row-context " +
            "cal-cell-label cal-cell-muted cal-gridline cal-hour-tick cal-now cal-lane-group cal-lane " +
            "cal-range-trigger cal-range-panel");
        var flat = css.Replace(" ", "").Replace("\n", "");

        Assert.Contains(".cal-viewport{", flat);
        Assert.Contains(".cal-lane{", flat);
        Assert.Contains(".cal-range-panel{", flat);
    }
}
