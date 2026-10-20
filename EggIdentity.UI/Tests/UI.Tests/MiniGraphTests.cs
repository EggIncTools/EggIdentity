using EggIdentity.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EggIdentity.UI.Tests;

public class MiniGraphTests {
    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    [Fact]
    public async Task SparklineDrawsALabelledPolyline() {
        var html = await RenderAsync<Sparkline>(new() { [nameof(Sparkline.Values)] = new double[] { 1, 3, 2 }, [nameof(Sparkline.Label)] = "Trend" });

        Assert.Contains("<svg", html);
        Assert.Contains("aria-label=\"Trend\"", html);
        Assert.Contains("<polyline", html);
        Assert.DoesNotContain("<path", html);
    }

    [Fact]
    public async Task SparklineAreaAddsAPath() {
        var html = await RenderAsync<Sparkline>(new() { [nameof(Sparkline.Values)] = new double[] { 1, 3, 2 }, [nameof(Sparkline.Area)] = true });

        Assert.Contains("<path", html);
    }

    [Fact]
    public async Task SparklineWithOnePointDrawsNothingInside() {
        var html = await RenderAsync<Sparkline>(new() { [nameof(Sparkline.Values)] = new double[] { 1 } });

        Assert.Contains("<svg", html);
        Assert.DoesNotContain("<polyline", html);
    }

    [Fact]
    public async Task StackedBarWidthsAreSharesOfTheSum() {
        var html = await RenderAsync<StackedBar>(new() { [nameof(StackedBar.Values)] = new double[] { 1, 3 }, [nameof(StackedBar.Label)] = "Rarity" });

        Assert.Contains("width:25%", html);
        Assert.Contains("width:75%", html);
        Assert.Contains("aria-label=\"Rarity\"", html);
        Assert.Equal(2, html.Split("mini-stack-seg").Length - 1);
    }

    [Fact]
    public async Task StackedBarColoursAreInlineForHexAndClassOtherwise() {
        var html = await RenderAsync<StackedBar>(new() {
            [nameof(StackedBar.Values)] = new double[] { 1, 1 },
            [nameof(StackedBar.Colors)] = new[] { "#ff0000", "bg-r-2" },
        });

        Assert.Contains("background-color:#ff0000", html);
        Assert.Contains("mini-stack-seg bg-r-2", html);
    }

    [Fact]
    public async Task StackedBarWithNoValuesRendersAnEmptyTrack() {
        var html = await RenderAsync<StackedBar>(new() { [nameof(StackedBar.Values)] = Array.Empty<double>() });

        Assert.Contains("mini-stack", html);
        Assert.DoesNotContain("mini-stack-seg", html);
    }

    [Fact]
    public async Task GaugeDrawsTrackAndFill() {
        var html = await RenderAsync<Gauge>(new() { [nameof(Gauge.Value)] = 3.0, [nameof(Gauge.Max)] = 4.0, [nameof(Gauge.Label)] = "Load" });

        Assert.Contains("<svg", html);
        Assert.Contains("mini-gauge-track", html);
        Assert.Contains("mini-gauge-fill", html);
        Assert.Contains("aria-label=\"Load\"", html);
    }
}
