using System.Globalization;

namespace EggIdentity.Reports;

public readonly record struct ChartPoint(double X, double Y, string Key, double Value, int Samples = 0);

public readonly record struct YTick(double Y, double Value);

public sealed record LineSeries(string Key, IReadOnlyList<ChartPoint> Points);

public static class ChartGeometry {
    public static double Max(IEnumerable<double?> values) {
        ArgumentNullException.ThrowIfNull(values);
        return values.Select(v => v ?? 0).DefaultIfEmpty(0).Max();
    }

    public static IReadOnlyList<ChartPoint> Points(IReadOnlyList<double?> values, IReadOnlyList<string> keys, ChartStyle style, double? max = null, IReadOnlyList<int>? samples = null) {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(style);
        if (values.Count < 2) return [];
        var top = Math.Max(max ?? Max(values), 1);
        var xStep = style.PlotWidth / (values.Count - 1);
        return [.. values.Select((v, i) => new ChartPoint(
            style.PadLeft + i * xStep,
            style.PadTop + style.PlotHeight * (1 - (v ?? 0) / top),
            i < keys.Count ? keys[i] : "",
            v ?? 0,
            samples is not null && i < samples.Count ? samples[i] : 0))];
    }

    public static IReadOnlyList<LineSeries> MultiLine(ReportMatrix matrix, ChartStyle style) {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(style);
        if (matrix.RowKeys.Count < 2) return [];
        var max = Max(matrix.Cells.SelectMany(r => r));
        return [.. matrix.ColKeys.Select((col, c) => new LineSeries(col, Points(
            [.. matrix.Cells.Select(r => r[c])], matrix.RowKeys, style, max, [.. matrix.Samples.Select(r => r[c])])))];
    }

    public static string Polyline(IReadOnlyList<ChartPoint> points) {
        ArgumentNullException.ThrowIfNull(points);
        return string.Join(' ', points.Select(p => Fmt(p.X) + "," + Fmt(p.Y)));
    }

    public static string AreaPath(IReadOnlyList<ChartPoint> points, ChartStyle style) {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(style);
        if (points.Count < 2) return "";
        var line = string.Join(' ', points.Select((p, i) => (i == 0 ? "M" : "L") + Fmt(p.X) + "," + Fmt(p.Y)));
        return $"{line} L{Fmt(points[^1].X)},{Fmt(style.Baseline)} L{Fmt(points[0].X)},{Fmt(style.Baseline)} Z";
    }

    public static IReadOnlyList<ChartPoint> ThinLabels(IReadOnlyList<ChartPoint> points, ChartStyle style) {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(style);
        if (points.Count <= style.MaxLabels || style.MaxLabels <= 0) return points;
        var step = (int)Math.Ceiling((double)points.Count / style.MaxLabels);
        return [.. points.Where((_, i) => i % step == 0 || i == points.Count - 1)];
    }

    public static IReadOnlyList<YTick> YTicks(double max, ChartStyle style) {
        ArgumentNullException.ThrowIfNull(style);
        if (max == 0) return [];
        return [.. style.TickFractions.Select(f => new YTick(style.PadTop + style.PlotHeight * (1 - f), max * f))];
    }

    public static string XLabel(string key, CultureInfo? culture = null) {
        ArgumentNullException.ThrowIfNull(key);
        var names = (culture ?? CultureInfo.InvariantCulture).DateTimeFormat.AbbreviatedMonthNames;
        if (DateOnly.TryParseExact(key, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ym))
            return string.Create(CultureInfo.InvariantCulture, $"{names[ym.Month - 1]} '{ym.Year % 100:00}");
        if (DateOnly.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ymd))
            return string.Create(CultureInfo.InvariantCulture, $"{ymd.Day} {names[ymd.Month - 1]}");
        return key.Length > 10 ? key[..9] + "..." : key;
    }

    public static double Percent(double value, double total) => total == 0 ? 0 : value / total * 100;

    public static string Fmt(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
