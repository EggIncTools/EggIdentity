using EggIdentity.Filters;

namespace EggIdentity.Reports;

internal static class CubeShaping {
    private const char Separator = '\u001f';

    public static bool Passes(CubeRow row, IReadOnlyList<HavingClause> having, IReadOnlyList<MeasureRequest> measures) {
        foreach (var clause in having) {
            var index = Index(measures, clause.MeasureKey);
            if (index < 0) continue;
            if (row.Values[index] is not { } v || !Holds(v.CompareTo(clause.Threshold), FilterOps.Canonical(clause.Op))) return false;
        }
        return true;
    }

    private static bool Holds(int cmp, string op) => op switch {
        FilterOps.Eq => cmp == 0,
        FilterOps.NotEq => cmp != 0,
        FilterOps.Gt => cmp > 0,
        FilterOps.Lt => cmp < 0,
        FilterOps.Gte => cmp >= 0,
        FilterOps.Lte => cmp <= 0,
        _ => true,
    };

    public static List<CubeRow> Normalize(
        List<CubeRow> rows, Normalize normalize, bool allowed, Func<IReadOnlyList<string>, double?> denominator) {
        if (!allowed) return rows;
        return normalize.Kind switch {
            NormalizeKind.Row => Share(rows, r => r.Keys.Count >= 2 ? r.Keys[0] : ""),
            NormalizeKind.Column => Share(rows, r => r.Keys.Count >= 2 ? string.Join(Separator, r.Keys.Skip(1)) : ""),
            NormalizeKind.Global => Share(rows, _ => ""),
            NormalizeKind.By => [.. rows.Select(r => r with { Values = [.. r.Values.Select(v => Ratio(v, denominator(r.Keys)))] })],
            _ => rows,
        };
    }

    private static double? Ratio(double? value, double? denominator) =>
        value is { } v && denominator is { } d && d != 0 ? v / d : null;

    private static List<CubeRow> Share(List<CubeRow> rows, Func<CubeRow, string> partition) {
        var width = rows.Count == 0 ? 0 : rows[0].Values.Count;
        var totals = rows.GroupBy(partition, StringComparer.Ordinal).ToDictionary(
            g => g.Key,
            g => Enumerable.Range(0, width).Select(i => g.Sum(r => r.Values[i] ?? 0)).ToArray(),
            StringComparer.Ordinal);
        return [.. rows.Select(r => {
            var total = totals[partition(r)];
            return r with { Values = [.. r.Values.Select((v, i) => Percent(v, total[i]))] };
        })];
    }

    private static double? Percent(double? value, double total) {
        if (value is not { } v) return null;
        return total == 0 ? 0 : v / total * 100;
    }

    public static List<CubeRow> Sort(List<CubeRow> rows, IReadOnlyList<SortKey> sort, IReadOnlyList<ReportDimension> dims, IReadOnlyList<MeasureRequest> measures) {
        var list = rows.ToList();
        list.Sort((a, b) => Compare(a, b, sort, dims, measures));
        return list;
    }

    private static int Compare(CubeRow a, CubeRow b, IReadOnlyList<SortKey> sort, IReadOnlyList<ReportDimension> dims, IReadOnlyList<MeasureRequest> measures) {
        foreach (var key in sort) {
            var cmp = CompareBy(a, b, key.Key, dims, measures);
            if (cmp != 0) return key.Descending ? -cmp : cmp;
        }
        for (var i = 0; i < dims.Count; i++) {
            var cmp = dims[i].Compare(a.Keys[i], b.Keys[i]);
            if (cmp != 0) return cmp;
        }
        return 0;
    }

    private static int CompareBy(CubeRow a, CubeRow b, string key, IReadOnlyList<ReportDimension> dims, IReadOnlyList<MeasureRequest> measures) {
        var m = Index(measures, key);
        if (m >= 0) return CompareValues(a.Values[m], b.Values[m]);
        for (var i = 0; i < dims.Count; i++)
            if (dims[i].Field.Answers(key)) return dims[i].Compare(a.Keys[i], b.Keys[i]);
        return 0;
    }

    private static int CompareValues(double? a, double? b) => (a, b) switch {
        (null, null) => 0,
        (null, _) => -1,
        (_, null) => 1,
        _ => a.Value.CompareTo(b.Value),
    };

    public static List<CubeRow> Limit(List<CubeRow> rows, int? limit, IReadOnlyList<SortKey> sort, IReadOnlyList<MeasureRequest> measures) {
        if (limit is not { } n || n < 0) return rows;
        if (rows.Count == 0 || rows[0].Keys.Count == 0) return [.. rows.Take(n)];
        var ranking = sort.FirstOrDefault(s => Index(measures, s.Key) >= 0);
        var measure = ranking is null ? 0 : Index(measures, ranking.Key);
        var descending = ranking?.Descending ?? true;
        var order = rows.Select((r, i) => (Key: r.Keys[0], Index: i))
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, First: g.Min(x => x.Index), Total: g.Sum(x => rows[x.Index].Values.Count > measure ? rows[x.Index].Values[measure] ?? 0 : 0)))
            .ToList();
        var ranked = descending
            ? order.OrderByDescending(x => x.Total).ThenBy(x => x.First)
            : order.OrderBy(x => x.Total).ThenBy(x => x.First);
        var keep = ranked.Take(n).Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        return [.. rows.Where(r => keep.Contains(r.Keys[0]))];
    }

    private static int Index(IReadOnlyList<MeasureRequest> measures, string key) {
        for (var i = 0; i < measures.Count; i++)
            if (string.Equals(measures[i].Key, key, StringComparison.Ordinal)) return i;
        return -1;
    }
}
