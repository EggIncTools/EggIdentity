namespace EggIdentity.Reports;

public sealed record HeatCell(string RowKey, string ColKey, double? Value, int Samples, double Intensity, bool Muted, bool Hidden);

public sealed record HeatmapLayout(IReadOnlyList<string> RowKeys, IReadOnlyList<string> RowLabels, IReadOnlyList<string> ColKeys, IReadOnlyList<string> ColLabels, IReadOnlyList<IReadOnlyList<HeatCell>> Cells);

public static class HeatmapGeometry {
    public static double Intensity(double value, double max, NormalizeKind normalize, ChartStyle style) {
        ArgumentNullException.ThrowIfNull(style);
        var raw = normalize switch {
            NormalizeKind.Row or NormalizeKind.Column or NormalizeKind.Global => value / 100,
            NormalizeKind.By => value > 0 ? Math.Min(value / style.HeatRatioSpan, 1) : 0,
            _ => max > 0 ? value / max : 0,
        };
        return Math.Clamp(Math.Max(style.HeatFloor, raw), 0, 1);
    }

    public static HeatmapLayout Layout(ReportMatrix matrix, NormalizeKind normalize, int? minSample, SampleRule rule, ChartStyle style) {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(style);
        var max = ChartGeometry.Max(matrix.Cells.SelectMany(r => r));
        var cells = matrix.RowKeys
            .Select((rk, r) => (IReadOnlyList<HeatCell>)[.. matrix.ColKeys.Select((ck, c) => Cell(matrix, r, c, max, normalize, minSample, rule, style))])
            .ToList();
        return new HeatmapLayout(matrix.RowKeys, matrix.RowLabels, matrix.ColKeys, matrix.ColLabels, cells);
    }

    private static HeatCell Cell(ReportMatrix matrix, int r, int c, double max, NormalizeKind normalize, int? minSample, SampleRule rule, ChartStyle style) {
        var value = matrix.Cells[r][c];
        var samples = matrix.Samples[r][c];
        var below = ReportProjection.BelowSample(samples, minSample) && rule != SampleRule.Show;
        var muted = below || (value ?? 0) == 0;
        var intensity = muted ? 0 : Intensity(value ?? 0, max, normalize, style);
        return new HeatCell(matrix.RowKeys[r], matrix.ColKeys[c], value, samples, intensity, muted, below && rule == SampleRule.Hide);
    }

    public static double RelativeLuminance(int r, int g, int b) =>
        0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);

    public static bool IsLight(string hex, ChartStyle style) {
        ArgumentNullException.ThrowIfNull(style);
        return ChartPalette.Rgb(hex) is { } c && RelativeLuminance(c.R, c.G, c.B) > style.ContrastLuminance;
    }

    public static string? Mix(string low, string high, double intensity) {
        if (ChartPalette.Rgb(low) is not { } a || ChartPalette.Rgb(high) is not { } b) return null;
        var t = Math.Clamp(intensity, 0, 1);
        return ChartPalette.Hex(
            (int)Math.Round(a.R + (b.R - a.R) * t),
            (int)Math.Round(a.G + (b.G - a.G) * t),
            (int)Math.Round(a.B + (b.B - a.B) * t));
    }

    private static double Linear(int c) {
        var s = c / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }
}
