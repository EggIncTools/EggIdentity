using EggIdentity.UI.Canvas;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EggIdentity.Canvas.Tests;

public class CanvasViewTests {
    private static async Task<string> RenderAsync(CanvasDoc doc) {
        await using var provider = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        var parameters = new Dictionary<string, object?> {
            [nameof(CanvasView.Doc)] = doc,
            [nameof(CanvasView.ItemBody)] = (RenderFragment<CanvasItem>)(item => b => b.AddContent(0, "body:" + item.Id)),
        };
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<CanvasView>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    [Fact]
    public async Task ItemsArePlacedInPercent() {
        var html = await RenderAsync(new CanvasDoc(4, 2, [new CanvasItem("a", "text", 3, 1, 2, 1, 1) { Rotation = 45, Style = new ItemStyle { Color = "#abc" } }]));

        Assert.Contains("left:50%", html);
        Assert.Contains("top:0%", html);
        Assert.Contains("width:50%", html);
        Assert.Contains("height:50%", html);
        Assert.Contains("z-index:1", html);
        Assert.Contains("rotate(45deg)", html);
        Assert.Contains("color:#abc", html);
        Assert.Contains("body:a", html);
    }

    [Fact]
    public async Task RootSetsTheUnitAndRatio() {
        var html = await RenderAsync(new CanvasDoc(4, 2, []));

        Assert.Contains("class=\"canvas-view\"", html);
        Assert.Contains("--canvas-unit:calc(100cqw / 4)", html);
        Assert.Contains("aspect-ratio:4 / 2", html);
    }

    [Fact]
    public async Task ViewHasNoEditorHooks() {
        var html = await RenderAsync(new CanvasDoc(4, 2, [new CanvasItem("a", "text", 1, 1, 1, 1, 0)]));

        Assert.DoesNotContain("canvas-handle", html);
        Assert.DoesNotContain("data-canvas", html);
        Assert.DoesNotContain("tabindex", html);
    }
}
