using System.Globalization;

namespace EggIdentity.Reports;

public sealed record GaugeLayout(double Cx, double Cy, double Radius, string TrackPath, string FillPath, double Fraction);

public static class GaugeGeometry {
    public static GaugeLayout Layout(double value, double max, double size, double sweepDegrees = 240, double thickness = 0.18) {
        var fraction = max <= 0 || !double.IsFinite(value) ? 0 : Math.Clamp(value / max, 0, 1);
        var (cx, cy) = (size / 2, size / 2);
        var r = size / 2 * (1 - thickness / 2);
        var sweep = Math.Clamp(sweepDegrees, 1, 359) * Math.PI / 180;
        var start = -Math.PI / 2 - sweep / 2;
        var track = Arc(cx, cy, r, start, start + sweep);
        var fill = fraction == 0 ? "" : Arc(cx, cy, r, start, start + sweep * fraction);
        return new GaugeLayout(cx, cy, r, track, fill, fraction);
    }

    private static string Arc(double cx, double cy, double r, double startAngle, double endAngle) {
        var large = endAngle - startAngle > Math.PI ? 1 : 0;
        return string.Format(
            CultureInfo.InvariantCulture,
            "M {0} {1} A {2} {3} 0 {4} 1 {5} {6}",
            ChartGeometry.Fmt(cx + Math.Cos(startAngle) * r), ChartGeometry.Fmt(cy + Math.Sin(startAngle) * r), ChartGeometry.Fmt(r), ChartGeometry.Fmt(r), large,
            ChartGeometry.Fmt(cx + Math.Cos(endAngle) * r), ChartGeometry.Fmt(cy + Math.Sin(endAngle) * r));
    }
}
