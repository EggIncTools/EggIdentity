using EggIdentity.Reports;

namespace EggIdentity.UI.Reports;

public sealed record ChartContext(ReportCube Cube, ReportDefinition Definition, IReportSource? Source, ChartStyle Style, ReportLabels Labels) {
    public IReadOnlyDictionary<string, string> SeriesStyles(IReadOnlyList<string> keys) {
        ArgumentNullException.ThrowIfNull(keys);
        var distinct = keys.Distinct(StringComparer.Ordinal).ToList();
        var resolved = ChartPalette.Resolve(distinct, Definition.View, Style);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < distinct.Count; i++) map[distinct[i]] = SeriesStyle(resolved[i]);
        return map;
    }

    private static string SeriesStyle(SeriesColor color) =>
        color.Override is { } hex && ReportCodec.IsHex(hex)
            ? $"--series:{hex}"
            : $"--series:var(--color-chart-{color.Slot},var(--color-accent))";
}
