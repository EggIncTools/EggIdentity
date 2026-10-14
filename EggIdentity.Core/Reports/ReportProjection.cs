namespace EggIdentity.Reports;

public sealed record ReportSeries(IReadOnlyList<string> Keys, IReadOnlyList<string> Labels, IReadOnlyList<double?> Values, IReadOnlyList<int> Samples);

public sealed record ReportMatrix(
    IReadOnlyList<string> RowKeys,
    IReadOnlyList<string> RowLabels,
    IReadOnlyList<string> ColKeys,
    IReadOnlyList<string> ColLabels,
    IReadOnlyList<IReadOnlyList<double?>> Cells,
    IReadOnlyList<IReadOnlyList<int>> Samples);

public enum SampleRule {
    Show,
    Dim,
    Hide,
}

public static class ReportProjection {
    public static ReportSeries Series(ReportCube cube, string measure, IReportSource? source = null) {
        ArgumentNullException.ThrowIfNull(cube);
        var m = Measure(cube, measure);
        if (cube.Dimensions.Count == 0) {
            var row = cube.Rows.Count > 0 ? cube.Rows[0] : null;
            return new ReportSeries([""], [""], [row?.Values[m]], [row?.Samples ?? 0]);
        }
        CheckCollapsible(cube, cube.Dimensions.Count - 1, cube.Measures[m]);
        var dim = Find(source, cube.Dimensions[0]);
        var groups = cube.Rows.GroupBy(r => r.Keys[0], StringComparer.Ordinal).ToList();
        var keys = groups.Select(g => g.Key).ToList();
        var values = groups.Select(g => Sum(g.Select(r => r.Values[m]))).ToList();
        var samples = groups.Select(g => g.Sum(r => r.Samples)).ToList();
        return new ReportSeries(keys, [.. keys.Select(k => Label(dim, k))], values, samples);
    }

    public static ReportMatrix Matrix(ReportCube cube, string measure, string rowDim, string colDim, IReportSource? source = null) {
        ArgumentNullException.ThrowIfNull(cube);
        var m = Measure(cube, measure);
        var r = Axis(cube, rowDim);
        var c = Axis(cube, colDim);
        CheckCollapsible(cube, cube.Dimensions.Count - 2, cube.Measures[m]);
        var rowKeys = cube.Rows.Select(x => x.Keys[r]).Distinct(StringComparer.Ordinal).ToList();
        var colKeys = cube.Rows.Select(x => x.Keys[c]).Distinct(StringComparer.Ordinal).ToList();
        var cells = new Dictionary<(string, string), (List<double?> Values, int Samples)>();
        foreach (var row in cube.Rows) {
            var at = (row.Keys[r], row.Keys[c]);
            if (!cells.TryGetValue(at, out var cell)) cell = (new List<double?>(), 0);
            cell.Values.Add(row.Values[m]);
            cells[at] = (cell.Values, cell.Samples + row.Samples);
        }
        var rd = Find(source, rowDim);
        var cd = Find(source, colDim);
        return new ReportMatrix(
            rowKeys, [.. rowKeys.Select(k => Label(rd, k))],
            colKeys, [.. colKeys.Select(k => Label(cd, k))],
            [.. rowKeys.Select(rk => (IReadOnlyList<double?>)[.. colKeys.Select(ck => cells.TryGetValue((rk, ck), out var cell) ? Sum(cell.Values) : null)])],
            [.. rowKeys.Select(rk => (IReadOnlyList<int>)[.. colKeys.Select(ck => cells.TryGetValue((rk, ck), out var cell) ? cell.Samples : 0)])]);
    }

    public static bool BelowSample(int samples, int? minSample) => minSample is { } min && min > 0 && samples < min;

    private static void CheckCollapsible(ReportCube cube, int extraDimensions, MeasureRequest measure) {
        if (extraDimensions <= 0) return;
        if (cube.Normalized)
            throw new InvalidOperationException($"measure \"{measure.Key}\" is normalized and cannot be collapsed across {extraDimensions} extra dimensions; query fewer dimensions");
        if (measure.Aggregation is not Aggregation.Count and not Aggregation.Sum)
            throw new InvalidOperationException($"measure \"{measure.Key}\" uses {measure.Aggregation} and cannot be collapsed across {extraDimensions} extra dimensions; query fewer dimensions");
    }

    private static double? Sum(IEnumerable<double?> values) {
        double? total = null;
        foreach (var v in values)
            if (v is { } x) total = (total ?? 0) + x;
        return total;
    }

    private static string Label(ReportDimension? dim, string key) => dim?.Label(key) ?? key;

    private static ReportDimension? Find(IReportSource? source, string key) => source?.Dimensions.FirstOrDefault(d => d.Field.Answers(key));

    private static int Measure(ReportCube cube, string measure) {
        ArgumentNullException.ThrowIfNull(measure);
        var index = cube.MeasureIndex(measure);
        return index >= 0 ? index : throw new ArgumentException($"measure \"{measure}\" is not in the cube", nameof(measure));
    }

    private static int Axis(ReportCube cube, string dimension) {
        ArgumentNullException.ThrowIfNull(dimension);
        var index = cube.DimensionIndex(dimension);
        return index >= 0 ? index : throw new ArgumentException($"dimension \"{dimension}\" is not in the cube", nameof(dimension));
    }
}
