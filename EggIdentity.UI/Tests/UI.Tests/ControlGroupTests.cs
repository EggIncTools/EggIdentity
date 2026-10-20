using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EggIdentity.UI.Tests;

public class ControlGroupTests {
    private enum Mode { Off, Row, Free }

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    private static Dictionary<string, object?> SegmentedParams(params SegmentedOption<Mode>[] options) => new() {
        [nameof(Segmented<>.Options)] = options,
        [nameof(Segmented<>.Selected)] = Mode.Row,
        [nameof(Segmented<>.AriaLabel)] = "Multi-select",
        [nameof(Segmented<>.Class)] = "host-seg",
    };

    [Fact]
    public async Task SegmentedMarksTheSelectedOptionAndLabelsTheGroup() {
        var html = await RenderAsync<Segmented<Mode>>(SegmentedParams(
            new SegmentedOption<Mode>(Mode.Off, "Off"),
            new SegmentedOption<Mode>(Mode.Row, "Row", Count: 1234)));

        Assert.Contains("class=\"segmented host-seg\" role=\"group\" aria-label=\"Multi-select\"", html);
        Assert.Contains("class=\"segmented-opt active\" aria-pressed=\"true\"", html);
        Assert.Contains("class=\"segmented-count\"", html);
        Assert.Single(html.Split("aria-pressed=\"true\"").Skip(1));
    }

    [Fact]
    public async Task DisabledOptionKeepsAFocusableTooltipAroundTheButton() {
        var html = await RenderAsync<Segmented<Mode>>(SegmentedParams(
            new SegmentedOption<Mode>(Mode.Row, "Row"),
            new SegmentedOption<Mode>(Mode.Free, "Free", Title: "Unavailable while sorting by drop count", Disabled: true)));

        var trigger = html.IndexOf("tooltip-trigger", StringComparison.Ordinal);
        var button = html.IndexOf("disabled", trigger, StringComparison.Ordinal);
        Assert.True(trigger >= 0 && button > trigger);
        Assert.Contains("tabindex=\"0\"", html[trigger..button]);
        Assert.Contains("Unavailable while sorting by drop count", html);
        Assert.DoesNotContain(" title=", html);
    }

    [Fact]
    public async Task ViewTabsSetsTheAccentAndPressedState() {
        var html = await RenderAsync<ViewTabs<Mode>>(new() {
            [nameof(ViewTabs<>.Tabs)] = new[] {
                new ViewTab<Mode>(Mode.Off, "Ships", "Missions", "--color-chart-2"),
                new ViewTab<Mode>(Mode.Row, "Drops", AccentVar: "red;}"),
            },
            [nameof(ViewTabs<>.Active)] = Mode.Off,
            [nameof(ViewTabs<>.AriaLabel)] = "View",
        });

        Assert.Contains("class=\"view-tab active\" aria-pressed=\"true\" style=\"--view-tab-accent:var(--color-chart-2)\"", html);
        Assert.Contains("aria-label=\"View\"", html);
        Assert.DoesNotContain("red;}", html);
    }

    [Theory]
    [InlineData(1.7, "--ratio:1")]
    [InlineData(-0.2, "--ratio:0")]
    [InlineData(0.625, "--ratio:0.625")]
    public void RatioVarIsClamped(double ratio, string expected) => Assert.Equal(expected, RatioStyle.Var(ratio));

    [Fact]
    public async Task RatioChipFormatsAndShades() {
        var shaded = await RenderAsync<RatioChip>(new() { [nameof(RatioChip.Ratio)] = 0.62, [nameof(RatioChip.Label)] = "Home" });
        var muted = await RenderAsync<RatioChip>(new() { [nameof(RatioChip.Ratio)] = 0.62, [nameof(RatioChip.Muted)] = true });

        Assert.Contains("ratio-fill ratio-ink", shaded);
        Assert.Contains("--ratio:0.62", shaded);
        Assert.Contains("ratio-chip-label", shaded);
        Assert.Contains("ratio-chip-muted", muted);
        Assert.DoesNotContain("ratio-fill", muted);
    }

    [Fact]
    public async Task TooltipRendersRowsWhenAnyLineHasAValue() {
        IReadOnlyList<TooltipLine> rows = [new("Airtime", Head: true), new("Home", Value: "62%", Ratio: 0.62), new("Virtue", Value: "38%")];
        IReadOnlyList<TooltipLine> plain = [new("Line one"), new("Line two")];

        var grid = await RenderAsync<Tooltip>(new() { [nameof(Tooltip.Lines)] = rows });
        var stacked = await RenderAsync<Tooltip>(new() { [nameof(Tooltip.Lines)] = plain });

        Assert.Contains("tooltip-rows", grid);
        Assert.Contains("tooltip-row-span", grid);
        Assert.Contains("class=\"tooltip-value ratio-ink\" style=\"--ratio:0.62\"", grid);
        Assert.DoesNotContain("tooltip-rows", stacked);
    }
}
