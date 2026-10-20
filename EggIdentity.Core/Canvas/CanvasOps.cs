using EggIdentity.Formatting;

namespace EggIdentity.Canvas;

public static class CanvasOps {
    public static CanvasDoc Add(CanvasDoc doc, CanvasItem item) {
        ArgumentNullException.ThrowIfNull(doc);
        ArgumentNullException.ThrowIfNull(item);
        if (doc.Items.Any(i => string.Equals(i.Id, item.Id, StringComparison.Ordinal))) throw new ArgumentException($"canvas item '{item.Id}' already exists", nameof(item));
        return doc with { Items = [.. doc.Items, Fit(doc, item with { Z = doc.Items.Count })] };
    }

    public static CanvasDoc Remove(CanvasDoc doc, string id) {
        ArgumentNullException.ThrowIfNull(doc);
        return Find(doc, id) is null ? doc : Renumber(doc with { Items = [.. Ordered(doc).Where(i => !Is(i, id))] });
    }

    public static CanvasDoc Move(CanvasDoc doc, string id, int x, int y) => Update(doc, id, i => Fit(doc, i with { X = x, Y = y }));

    public static CanvasDoc Nudge(CanvasDoc doc, string id, int dx, int dy) => Update(doc, id, i => Fit(doc, i with { X = i.X + dx, Y = i.Y + dy }));

    public static CanvasDoc Resize(CanvasDoc doc, string id, int w, int h) => Update(doc, id, i => {
        var snap = Math.Max(1, doc.Snap);
        return i with { W = Math.Clamp(SnapUp(w, snap), 1, doc.Cols - i.X + 1), H = Math.Clamp(SnapUp(h, snap), 1, doc.Rows - i.Y + 1) };
    });

    public static CanvasDoc BringForward(CanvasDoc doc, string id) => Shift(doc, id, 1);

    public static CanvasDoc SendBack(CanvasDoc doc, string id) => Shift(doc, id, -1);

    public static CanvasDoc BringToFront(CanvasDoc doc, string id) {
        ArgumentNullException.ThrowIfNull(doc);
        return Find(doc, id) is { } item ? Renumber(doc with { Items = [.. Ordered(doc).Where(i => !Is(i, id)), item] }) : doc;
    }

    public static CanvasDoc SendToBack(CanvasDoc doc, string id) {
        ArgumentNullException.ThrowIfNull(doc);
        return Find(doc, id) is { } item ? Renumber(doc with { Items = [item, .. Ordered(doc).Where(i => !Is(i, id))] }) : doc;
    }

    public static CanvasDoc Duplicate(CanvasDoc doc, string id, string newId) {
        ArgumentNullException.ThrowIfNull(doc);
        var source = Find(doc, id);
        return source is null ? doc : Add(doc, source with { Id = newId, X = source.X + doc.Snap, Y = source.Y + doc.Snap });
    }

    public static CanvasDoc Clamp(CanvasDoc doc) {
        ArgumentNullException.ThrowIfNull(doc);
        return Renumber(doc with { Items = [.. Ordered(doc).Select(i => Fit(doc, i))] });
    }

    public static CanvasDoc Clean(CanvasDoc doc) {
        var clamped = Clamp(doc);
        return clamped with { Items = [.. clamped.Items.Select(i => i.Style is null ? i : i with { Style = Clean(i.Style) })] };
    }

    public static ItemStyle Clean(ItemStyle style) {
        ArgumentNullException.ThrowIfNull(style);
        return style with {
            Border = HexColor.Valid(style.Border),
            Background = HexColor.Valid(style.Background),
            Color = HexColor.Valid(style.Color),
            Opacity = style.Opacity is >= 0 and <= 1 ? style.Opacity : null,
            FontWeight = style.FontWeight is >= 100 and <= 900 ? style.FontWeight : null,
            BorderWidth = style.BorderWidth is >= 0 ? style.BorderWidth : null,
            Radius = style.Radius is >= 0 ? style.Radius : null,
            Padding = style.Padding is >= 0 ? style.Padding : null,
            FontSize = style.FontSize is > 0 ? style.FontSize : null,
        };
    }

    private static CanvasDoc Shift(CanvasDoc doc, string id, int direction) {
        ArgumentNullException.ThrowIfNull(doc);
        var ordered = Ordered(doc).ToList();
        var index = ordered.FindIndex(i => Is(i, id));
        var target = index + direction;
        if (index < 0 || target < 0 || target >= ordered.Count) return doc;
        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        return Renumber(doc with { Items = ordered });
    }

    private static CanvasDoc Update(CanvasDoc doc, string id, Func<CanvasItem, CanvasItem> change) {
        ArgumentNullException.ThrowIfNull(doc);
        return Find(doc, id) is null ? doc : doc with { Items = [.. doc.Items.Select(i => Is(i, id) ? change(i) : i)] };
    }

    private static CanvasItem Fit(CanvasDoc doc, CanvasItem item) {
        var snap = Math.Max(1, doc.Snap);
        var w = Math.Clamp(SnapUp(item.W, snap), 1, doc.Cols);
        var h = Math.Clamp(SnapUp(item.H, snap), 1, doc.Rows);
        var x = Math.Clamp(SnapOrigin(item.X, snap), 1, Math.Max(1, doc.Cols - w + 1));
        var y = Math.Clamp(SnapOrigin(item.Y, snap), 1, Math.Max(1, doc.Rows - h + 1));
        return item with { X = x, Y = y, W = w, H = h };
    }

    private static int SnapOrigin(int cell, int snap) => (int)Math.Round((cell - 1) / (double)snap, MidpointRounding.AwayFromZero) * snap + 1;

    private static int SnapUp(int size, int snap) => Math.Max(snap, (int)Math.Round(size / (double)snap, MidpointRounding.AwayFromZero) * snap);

    private static CanvasDoc Renumber(CanvasDoc doc) => doc with { Items = [.. doc.Items.Select((i, z) => i.Z == z ? i : i with { Z = z })] };

    private static IEnumerable<CanvasItem> Ordered(CanvasDoc doc) => doc.Items.OrderBy(i => i.Z);

    private static CanvasItem? Find(CanvasDoc doc, string id) => doc.Items.FirstOrDefault(i => Is(i, id));

    private static bool Is(CanvasItem item, string id) => string.Equals(item.Id, id, StringComparison.Ordinal);
}
