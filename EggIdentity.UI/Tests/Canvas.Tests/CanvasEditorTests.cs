using EggIdentity.UI.Canvas;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace EggIdentity.Canvas.Tests;

public class CanvasEditorTests {
    private sealed class NullJsRuntime : IJSRuntime {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }

    private static readonly IReadOnlyList<CanvasPaletteEntry> Palette = [new CanvasPaletteEntry("text", "Text", "type", 2, 1)];

    private static async Task<string> RenderAsync(Dictionary<string, object?> parameters) {
        await using var provider = new ServiceCollection().AddSingleton<IJSRuntime, NullJsRuntime>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        parameters[nameof(CanvasEditor.ItemBody)] = (RenderFragment<CanvasItem>)(item => b => b.AddContent(0, "body:" + item.Id));
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<CanvasEditor>(ParameterView.FromDictionary(parameters))).ToHtmlString());
    }

    private static Dictionary<string, object?> Editor(CanvasDoc doc) => new() {
        [nameof(CanvasEditor.Doc)] = doc,
        [nameof(CanvasEditor.Palette)] = Palette,
        [nameof(CanvasEditor.CellSize)] = 24,
    };

    [Fact]
    public async Task EmptyDocRendersAnEmptyStateAndThePalette() {
        var html = await RenderAsync(Editor(new CanvasDoc(4, 2, [])));

        Assert.Contains("class=\"empty\"", html);
        Assert.Contains("canvas-palette-item", html);
        Assert.Contains("Text", html);
    }

    [Fact]
    public async Task ItemsArePlacedInCells() {
        var html = await RenderAsync(Editor(new CanvasDoc(4, 2, [new CanvasItem("a", "text", 3, 2, 2, 1, 3)])));

        Assert.Contains("left:48px", html);
        Assert.Contains("top:24px", html);
        Assert.Contains("width:48px", html);
        Assert.Contains("height:24px", html);
        Assert.Contains("z-index:3", html);
        Assert.Contains("--canvas-unit:24px", html);
        Assert.Contains("body:a", html);
    }

    [Fact]
    public async Task SelectedItemHasHandlesAndReadOnlyHasNone() {
        var doc = new CanvasDoc(4, 2, [new CanvasItem("a", "text", 1, 1, 1, 1, 0)]);
        var selected = Editor(doc);
        selected[nameof(CanvasEditor.Selected)] = "a";
        var readOnly = Editor(doc);
        readOnly[nameof(CanvasEditor.Selected)] = "a";
        readOnly[nameof(CanvasEditor.ReadOnly)] = true;

        var selectedHtml = await RenderAsync(selected);
        var readOnlyHtml = await RenderAsync(readOnly);

        Assert.Equal(8, selectedHtml.Split("canvas-handle ").Length - 1);
        Assert.Contains("canvas-item selected", selectedHtml);
        Assert.DoesNotContain("canvas-handle", readOnlyHtml);
        Assert.DoesNotContain("canvas-palette", readOnlyHtml);
    }

    [Fact]
    public async Task PreviewSlotRendersWhenSupplied() {
        var parameters = Editor(new CanvasDoc(4, 2, []));
        parameters[nameof(CanvasEditor.Preview)] = (RenderFragment)(b => b.AddContent(0, "preview-here"));

        var html = await RenderAsync(parameters);

        Assert.Contains("canvas-preview", html);
        Assert.Contains("preview-here", html);
    }
}
