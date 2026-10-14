using System.Globalization;

namespace EggIdentity.Reports;

public sealed record PieItem(string Key, string Label, double Value, int Samples);

public sealed record PieSlice(string Key, string Label, double Value, double Pct, string Path, double MidAngle, int Samples);

public sealed record PieLayout(double Width, double Height, double Cx, double Cy, double Radius, IReadOnlyList<PieSlice> Slices);

public static class PieGeometry {
    public static IReadOnlyList<PieItem> Items(ReportSeries series, ChartStyle style, string otherLabel) {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(style);
        var items = series.Keys.Select((k, i) => new PieItem(k, series.Labels[i], series.Values[i] ?? 0, series.Samples[i])).ToList();
        if (items.Sum(i => i.Value) == 0) return [];
        if (items.Count <= style.PieMaxSegments || style.PieMaxSegments < 2) return items;
        var sorted = items.OrderByDescending(x => x.Value).ToList();
        var keep = style.PieMaxSegments - 1;
        return [.. sorted.Take(keep), new PieItem(BarGeometry.OtherKey, otherLabel, sorted.Skip(keep).Sum(x => x.Value), sorted.Skip(keep).Sum(x => x.Samples))];
    }

    public static PieLayout Layout(ReportSeries series, ChartStyle style, string otherLabel) {
        ArgumentNullException.ThrowIfNull(style);
        var size = style.PieSize;
        var (cx, cy, r) = (size / 2, size / 2, size * style.PieRadius);
        var items = Items(series, style, otherLabel);
        var total = items.Sum(i => i.Value);
        var slices = new List<PieSlice>(items.Count);
        var angle = -Math.PI / 2;
        foreach (var item in items) {
            var sweep = total == 0 ? 0 : item.Value / total * 2 * Math.PI;
            var start = angle;
            angle += sweep;
            slices.Add(new PieSlice(item.Key, item.Label, item.Value, ChartGeometry.Percent(item.Value, total), SlicePath(cx, cy, r, start, angle), (start + angle) / 2, item.Samples));
        }
        return new PieLayout(size, size, cx, cy, r, slices);
    }

    public static string SlicePath(double cx, double cy, double r, double startAngle, double endAngle) {
        var large = endAngle - startAngle > Math.PI ? 1 : 0;
        return string.Format(
            CultureInfo.InvariantCulture,
            "M {0} {1} L {2} {3} A {4} {5} 0 {6} 1 {7} {8} Z",
            cx, cy, cx + Math.Cos(startAngle) * r, cy + Math.Sin(startAngle) * r, r, r, large, cx + Math.Cos(endAngle) * r, cy + Math.Sin(endAngle) * r);
    }

    public static (double X, double Y) SlicePoint(PieLayout layout, PieSlice slice, ChartStyle style) {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(style);
        return (layout.Cx + Math.Cos(slice.MidAngle) * layout.Radius * style.PieLabelInset,
            layout.Cy + Math.Sin(slice.MidAngle) * layout.Radius * style.PieLabelInset);
    }
}
