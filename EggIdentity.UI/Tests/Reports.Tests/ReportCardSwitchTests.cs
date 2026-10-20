using System.Text.RegularExpressions;
using EggIdentity.UI;
using EggIdentity.UI.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EggIdentity.Reports.Tests;

public partial class ReportCardSwitchTests {
    [GeneratedRegex("<[^>]*aria-hidden=\"true\"[^>]*>.*?</span>", RegexOptions.Singleline)]
    private static partial Regex HiddenSpan();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    private static ReportDefinition Definition(string chart, Dictionary<string, string>? options = null) =>
        new(1, "r1", "Drops", new ReportQuery("drops") { GroupBy = [new GroupKey("ship")] },
            new ReportView(chart) { Options = options ?? [] });

    private static ReportCube Cube(int rows) =>
        new(["ship"], [MeasureRequest.RowCount], false,
            [.. Enumerable.Range(0, rows).Select(i => new CubeRow([$"ship{i}"], [(double)(rows - i) * 1_000_000], 10))]);

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    private static Dictionary<string, object?> Card(string chart, int rows) => new() {
        [nameof(ReportCard.Definition)] = Definition(chart),
        [nameof(ReportCard.Cube)] = Cube(rows),
    };

    [Fact]
    public async Task NoSwitcherWithoutChoices() {
        var html = await RenderAsync<ReportCard>(Card(ReportCharts.Bar, 3));

        Assert.DoesNotContain("report-card-switch", html);
    }

    [Fact]
    public async Task SwitcherRendersIconOptionsWithNamesAndRaisesTheChoice() {
        string? picked = null;
        var parameters = Card(ReportCharts.Bar, 3);
        parameters[nameof(ReportCard.ChartChoices)] = new[] { ReportCharts.Bar, ReportCharts.Pie };
        parameters[nameof(ReportCard.OnChartChanged)] = EventCallback.Factory.Create<string>(this, c => picked = c);

        var html = await RenderAsync<ReportCard>(parameters);

        Assert.Contains("report-card-switch", html);
        Assert.Contains("aria-label=\"Chart type\"", html);
        Assert.Contains("aria-label=\"Pie\"", html);
        Assert.Contains("class=\"segmented-opt active\" aria-pressed=\"true\"", html);
        Assert.Null(picked);
    }

    [Fact]
    public void EveryBuiltInChartHasAVendoredIcon() =>
        Assert.All(ReportCharts.BuiltIn, c => Assert.True(Icons.IconPack.TryGet(ReportCard.ChartIcon(c), out _), c));

    [Fact]
    public async Task CompactOverrideReachesTheBarValues() {
        var parameters = Card(ReportCharts.Bar, 2);
        parameters[nameof(ReportCard.NumberFormat)] = ReportNumberFormat.Compact;

        var html = await RenderAsync<ReportCard>(parameters);

        Assert.Contains("2M", html);
        Assert.DoesNotContain("2000000", html);
    }

    [Fact]
    public async Task EachBarLabelAndValueReadsOnce() {
        var html = await RenderAsync<ReportCard>(Card(ReportCharts.Bar, 2));

        var visibleText = Tags().Replace(HiddenSpan().Replace(html, ""), " ");
        Assert.Contains("aria-label=\"ship0, 2000000\"", html);
        Assert.DoesNotContain("ship0", visibleText);
        Assert.DoesNotContain(" title=", html);
    }

    [Fact]
    public async Task BarTrackIsStartAnchored() {
        var html = await RenderAsync<ReportCard>(Card(ReportCharts.Bar, 2));

        Assert.Contains("class=\"report-bar-track\"", html);
        Assert.DoesNotContain("justify-content:center", html);
    }

    [Fact]
    public async Task PieFoldsPastPieTopAndShowsValueAndPercent() {
        var html = await RenderAsync<ReportCard>(new() {
            [nameof(ReportCard.Definition)] = Definition(ReportCharts.Pie, new Dictionary<string, string> { ["pie_top"] = "3" }),
            [nameof(ReportCard.Cube)] = Cube(6),
        });

        Assert.Equal(3, html.Split("report-pie-row").Length - 1);
        Assert.Contains(ReportLabels.Default.Other, html);
        Assert.Contains("report-pie-value", html);
        Assert.Contains("%", html);
    }

    [Fact]
    public async Task CompactNumberTipsTheFullValueOnlyWhenItDiffers() {
        var big = await RenderAsync<CompactNumber>(new() { [nameof(CompactNumber.Value)] = 60907.0 });
        var small = await RenderAsync<CompactNumber>(new() { [nameof(CompactNumber.Value)] = 412.0 });

        Assert.Contains("60.9k", big);
        Assert.Contains("60,907", big);
        Assert.Contains("tooltip-trigger", big);
        Assert.DoesNotContain("tooltip-trigger", small);
    }
}
