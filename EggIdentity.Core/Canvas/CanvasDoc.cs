using System.Globalization;
using System.Text;

namespace EggIdentity.Canvas;

public enum TextAlign {
    Start,
    Center,
    End,
}

public sealed record ItemStyle {
    public string? Border { get; init; }
    public double? BorderWidth { get; init; }
    public string? Background { get; init; }
    public double? Radius { get; init; }
    public double? Padding { get; init; }
    public double? Opacity { get; init; }
    public string? Color { get; init; }
    public double? FontSize { get; init; }
    public int? FontWeight { get; init; }
    public TextAlign? Align { get; init; }

    public string Css(double? unit = null) {
        var sb = new StringBuilder();
        if (Border is not null) Append(sb, "border", Length(BorderWidth ?? 1, unit) + " solid " + Border);
        if (Background is not null) Append(sb, "background-color", Background);
        if (Radius is { } radius) Append(sb, "border-radius", Length(radius, unit));
        if (Padding is { } padding) Append(sb, "padding", Length(padding, unit));
        if (Opacity is { } opacity) Append(sb, "opacity", Num(opacity));
        if (Color is not null) Append(sb, "color", Color);
        if (FontSize is { } size) Append(sb, "font-size", Length(size, unit));
        if (FontWeight is { } weight) Append(sb, "font-weight", weight.ToString(CultureInfo.InvariantCulture));
        if (Align is { } align) Append(sb, "text-align", align switch { TextAlign.Center => "center", TextAlign.End => "end", _ => "start" });
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string property, string value) {
        if (sb.Length > 0) sb.Append(';');
        sb.Append(property).Append(':').Append(value);
    }

    private static string Length(double units, double? unit) =>
        unit is { } px ? Num(units * px) + "px" : "calc(" + Num(units) + " * var(--canvas-unit))";

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed record CanvasItem(string Id, string Kind, int X, int Y, int W, int H, int Z) {
    public int? Rotation { get; init; }
    public ItemStyle? Style { get; init; }
    public string? Payload { get; init; }
}

public sealed record CanvasDoc(int Cols, int Rows, IReadOnlyList<CanvasItem> Items) {
    public int Snap { get; init; } = 1;
    public int Version { get; init; } = 1;

    public bool Equals(CanvasDoc? other) =>
        other is not null && Cols == other.Cols && Rows == other.Rows && Snap == other.Snap && Version == other.Version && Items.SequenceEqual(other.Items);

    public override int GetHashCode() {
        var hash = new HashCode();
        hash.Add(Cols);
        hash.Add(Rows);
        hash.Add(Snap);
        hash.Add(Version);
        foreach (var item in Items) hash.Add(item);
        return hash.ToHashCode();
    }
}
