using EggIdentity.Reports;

namespace EggIdentity.UI.Reports;

public sealed record ChartContext(ReportCube Cube, ReportDefinition Definition, IReportSource? Source, ChartStyle Style, ReportLabels Labels) {
    public IReadOnlyDictionary<string, string> SeriesStyles(IReadOnlyList<string> keys, bool series = false) {
        ArgumentNullException.ThrowIfNull(keys);
        var distinct = keys.Distinct(StringComparer.Ordinal).ToList();
        var resolved = ChartPalette.Resolve(distinct, Definition.View, Style, series ? Style.SeriesLightness : null);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < distinct.Count; i++)
            map[distinct[i]] = series && distinct[i] == BarGeometry.OtherKey && Override(distinct[i]) is null ? OtherStyle : SeriesStyle(resolved[i]);
        return map;
    }

    public string Compact(string measureKey, double value) {
        var format = Format(measureKey);
        return format.Kind == ValueFormatKind.Decimal && value % 1 == 0
            ? value.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            : format.Format(value);
    }

    private string? Override(string key) =>
        Definition.View.LabelColors.TryGetValue(key, out var own) && ReportCodec.IsHex(own) ? own : null;

    public ValueFormat Format(string measureKey) {
        ArgumentNullException.ThrowIfNull(measureKey);
        if (Cube.Normalized) {
            return Definition.Query.Normalize.Kind == NormalizeKind.By ? ValueFormat.Decimal(2) : ValueFormat.Percent;
        }
        var format = Source?.Measure(measureKey)?.Format
            ?? (measureKey == MeasureRequest.RowCountKey ? ValueFormat.Integer : ValueFormat.Decimal(2));
        return Definition.View.Resolve(format);
    }

    public string Label(string key, string fallback) {
        ArgumentNullException.ThrowIfNull(key);
        return Definition.View.Labels.TryGetValue(key, out var custom) ? custom : fallback;
    }

    private const string OtherStyle = "--series:var(--report-other,var(--color-muted))";

    private static string SeriesStyle(SeriesColor color) =>
        color.Override is { } hex && ReportCodec.IsHex(hex)
            ? $"--series:{hex}"
            : $"--series:var(--color-chart-{color.Slot},var(--color-accent))";
}
