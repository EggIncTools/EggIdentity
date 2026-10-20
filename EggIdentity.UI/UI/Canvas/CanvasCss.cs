using System.Globalization;
using EggIdentity.Canvas;

namespace EggIdentity.UI.Canvas;

internal static class CanvasCss {
    internal static string Percent(CanvasDoc doc, CanvasItem item) =>
        string.Create(CultureInfo.InvariantCulture,
            $"left:{Pct(item.X - 1, doc.Cols)};top:{Pct(item.Y - 1, doc.Rows)};width:{Pct(item.W, doc.Cols)};height:{Pct(item.H, doc.Rows)};{Common(item, null)}");

    internal static string Pixels(CanvasItem item, int cell) =>
        string.Create(CultureInfo.InvariantCulture,
            $"left:{(item.X - 1) * cell}px;top:{(item.Y - 1) * cell}px;width:{item.W * cell}px;height:{item.H * cell}px;{Common(item, cell)}");

    private static string Common(CanvasItem item, double? unit) {
        var css = string.Create(CultureInfo.InvariantCulture, $"z-index:{item.Z}");
        if (item.Rotation is { } deg and not 0) css += string.Create(CultureInfo.InvariantCulture, $";transform:rotate({deg}deg)");
        var style = item.Style?.Css(unit);
        return string.IsNullOrEmpty(style) ? css : css + ";" + style;
    }

    private static string Pct(int cells, int total) => (cells * 100.0 / Math.Max(1, total)).ToString("0.###", CultureInfo.InvariantCulture) + "%";
}
