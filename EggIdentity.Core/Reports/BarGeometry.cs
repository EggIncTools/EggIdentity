namespace EggIdentity.Reports;

public sealed record BarItem(string Key, string Label, double Value, double Width, int Samples);

public sealed record GroupedBarCell(string ColKey, double Value, double Width, int Samples);

public sealed record GroupedBarRow(string Key, string Label, IReadOnlyList<GroupedBarCell> Cells);

public sealed record GroupedBarLayout(IReadOnlyList<string> ColKeys, IReadOnlyList<string> ColLabels, IReadOnlyList<GroupedBarRow> Rows, bool HasOther);

public enum BarOrder {
    Natural,
    Descending,
    Ascending,
}

public static class BarGeometry {
    public const string OtherKey = "\u0000other";

    public static IReadOnlyList<BarItem> Bars(ReportSeries series, BarOrder order = BarOrder.Natural) {
        ArgumentNullException.ThrowIfNull(series);
        var max = Math.Max(ChartGeometry.Max(series.Values), 1);
        var items = series.Keys.Select((k, i) => {
            var v = series.Values[i] ?? 0;
            return new BarItem(k, series.Labels[i], v, Math.Round(v / max * 100), series.Samples[i]);
        });
        return order switch {
            BarOrder.Descending => [.. items.OrderByDescending(b => b.Value)],
            BarOrder.Ascending => [.. items.OrderBy(b => b.Value)],
            _ => [.. items],
        };
    }

    public static GroupedBarLayout Grouped(ReportMatrix matrix, ChartStyle style, bool percent, string otherLabel) {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(style);
        var limit = style.GroupedMaxCols;
        var hasOther = matrix.ColKeys.Count > limit + 1;
        var kept = hasOther ? limit : matrix.ColKeys.Count;
        IReadOnlyList<string> colKeys = hasOther ? [.. matrix.ColKeys.Take(kept), OtherKey] : matrix.ColKeys;
        IReadOnlyList<string> colLabels = hasOther ? [.. matrix.ColLabels.Take(kept), otherLabel] : matrix.ColLabels;
        var max = ChartGeometry.Max(matrix.Cells.SelectMany(r => r));
        var rows = matrix.RowKeys.Select((rk, r) => {
            var cells = matrix.Cells[r];
            var cellSamples = matrix.Samples[r];
            var values = Enumerable.Range(0, kept).Select(c => cells[c] ?? 0).ToList();
            var samples = Enumerable.Range(0, kept).Select(c => cellSamples[c]).ToList();
            if (hasOther) {
                values.Add(cells.Skip(kept).Sum(v => v ?? 0));
                samples.Add(cellSamples.Skip(kept).Sum());
            }
            return new GroupedBarRow(rk, matrix.RowLabels[r], [.. values
                .Select((v, c) => new GroupedBarCell(colKeys[c], v, percent ? v : ChartGeometry.Percent(v, max), samples[c]))
                .Where(cell => cell.Value > 0)]);
        });
        return new GroupedBarLayout(colKeys, colLabels, [.. rows], hasOther);
    }
}
