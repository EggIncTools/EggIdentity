using System.Globalization;
using EggIdentity.Filters;

namespace EggIdentity.Reports;

public sealed class SnapshotColumn(string key, FilterValueKind kind, int rows) {
    public string Key { get; } = key;

    public FilterValueKind Kind { get; } = kind;

    public ulong[] Present { get; set; } = new ulong[(rows + 63) / 64];

    public double[]? Numbers { get; set; }

    public long[]? Dates { get; set; }

    public bool[]? Bools { get; set; }

    public int[]? Text { get; set; }

    public int[]? ManyOffsets { get; set; }

    public int[]? ManyItems { get; set; }

    public string[] Dictionary { get; set; } = [];

    public bool IsPresent(int row) => (Present[row >> 6] & (1UL << (row & 63))) != 0;

    public FilterSubjectValue At(int row) {
        if (!IsPresent(row)) return FilterSubjectValue.Absent;
        return Kind switch {
            FilterValueKind.Number => FilterSubjectValue.Of(Numbers![row]),
            FilterValueKind.Date => FilterSubjectValue.Of(DateTimeOffset.FromUnixTimeSeconds(Dates![row])),
            FilterValueKind.Bool => FilterSubjectValue.Of(Bools![row]),
            FilterValueKind.Set => FilterSubjectValue.Of(Many(row)),
            _ => FilterSubjectValue.Of(Dictionary[Text![row]]),
        };
    }

    internal void Mark(int row) => Present[row >> 6] |= 1UL << (row & 63);

    private IEnumerable<string> Many(int row) {
        var start = ManyOffsets![row];
        var end = ManyOffsets[row + 1];
        for (var i = start; i < end; i++) yield return Dictionary[ManyItems![i]];
    }
}

public sealed class RowSnapshot(string source, string version, int rows, IReadOnlyList<SnapshotColumn> columns) {
    public string Source { get; } = source;

    public string Version { get; } = version;

    public int Rows { get; } = rows;

    public IReadOnlyList<SnapshotColumn> Columns { get; } = columns;

    public SnapshotColumn? Column(string key) {
        ArgumentNullException.ThrowIfNull(key);
        return Columns.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
    }

    public static RowSnapshot Capture<TRow>(ReportSource<TRow> source, IReadOnlyList<TRow> rows, string version) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var keys = CaptureKeys(source);
        var columns = new List<SnapshotColumn>(keys.Count);
        foreach (var key in keys) {
            var values = new FilterSubjectValue[rows.Count];
            for (var r = 0; r < rows.Count; r++) values[r] = source.Value(rows[r], key);
            var kind = source.Dimension(key) is { } dimension ? ColumnKind(dimension.Field.Kind) : InferKind(values);
            columns.Add(BuildColumn(key, kind, values));
        }

        return new RowSnapshot(source.Id, version, rows.Count, columns);
    }

    private static List<string> CaptureKeys<TRow>(ReportSource<TRow> source) {
        var keys = new List<string>();
        void Add(string? key) {
            if (key is not null && !keys.Contains(key, StringComparer.Ordinal)) keys.Add(key);
        }
        foreach (var dimension in source.Dimensions) Add(dimension.Key);
        foreach (var measure in source.Measures) {
            Add(measure.Key);
            Add(measure.DistinctBy);
        }
        Add(source.SampleKey);
        return keys;
    }

    private static FilterValueKind ColumnKind(FilterValueKind fieldKind) => fieldKind switch {
        FilterValueKind.Number => FilterValueKind.Number,
        FilterValueKind.Bool => FilterValueKind.Bool,
        FilterValueKind.Date => FilterValueKind.Date,
        FilterValueKind.Set => FilterValueKind.Set,
        _ => FilterValueKind.Text,
    };

    private static FilterValueKind InferKind(FilterSubjectValue[] values) {
        foreach (var value in values) {
            switch (value) {
                case FilterSubjectValue.NumberValue: return FilterValueKind.Number;
                case FilterSubjectValue.TextValue: return FilterValueKind.Text;
                case FilterSubjectValue.BoolValue: return FilterValueKind.Bool;
                case FilterSubjectValue.DateValue: return FilterValueKind.Date;
                case FilterSubjectValue.ManyValue: return FilterValueKind.Set;
            }
        }
        return FilterValueKind.Number;
    }

    private static SnapshotColumn BuildColumn(string key, FilterValueKind kind, FilterSubjectValue[] values) {
        var column = new SnapshotColumn(key, kind, values.Length);
        switch (kind) {
            case FilterValueKind.Number:
                BuildNumbers(column, values);
                break;
            case FilterValueKind.Date:
                BuildDates(column, values);
                break;
            case FilterValueKind.Bool:
                BuildBools(column, values);
                break;
            case FilterValueKind.Set:
                BuildMany(column, values);
                break;
            default:
                BuildText(column, values);
                break;
        }
        return column;
    }

    private static void BuildNumbers(SnapshotColumn column, FilterSubjectValue[] values) {
        var numbers = new double[values.Length];
        for (var i = 0; i < values.Length; i++) {
            if (values[i] is not FilterSubjectValue.NumberValue n) continue;
            column.Mark(i);
            numbers[i] = n.Value;
        }
        column.Numbers = numbers;
    }

    private static void BuildDates(SnapshotColumn column, FilterSubjectValue[] values) {
        var dates = new long[values.Length];
        for (var i = 0; i < values.Length; i++) {
            if (values[i] is not FilterSubjectValue.DateValue d) continue;
            column.Mark(i);
            dates[i] = d.Value.ToUnixTimeSeconds();
        }
        column.Dates = dates;
    }

    private static void BuildBools(SnapshotColumn column, FilterSubjectValue[] values) {
        var bools = new bool[values.Length];
        for (var i = 0; i < values.Length; i++) {
            if (values[i] is not FilterSubjectValue.BoolValue b) continue;
            column.Mark(i);
            bools[i] = b.Value;
        }
        column.Bools = bools;
    }

    private static void BuildText(SnapshotColumn column, FilterSubjectValue[] values) {
        var dictionary = new List<string>();
        var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var text = new int[values.Length];
        for (var i = 0; i < values.Length; i++) {
            if (TextOf(values[i]) is not { } s) continue;
            column.Mark(i);
            if (!indexOf.TryGetValue(s, out var index)) {
                index = dictionary.Count;
                dictionary.Add(s);
                indexOf[s] = index;
            }
            text[i] = index;
        }
        column.Text = text;
        column.Dictionary = [.. dictionary];
    }

    private static void BuildMany(SnapshotColumn column, FilterSubjectValue[] values) {
        var dictionary = new List<string>();
        var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var offsets = new int[values.Length + 1];
        var items = new List<int>();
        for (var i = 0; i < values.Length; i++) {
            offsets[i] = items.Count;
            if (values[i] is not FilterSubjectValue.ManyValue m) continue;
            column.Mark(i);
            foreach (var v in m.Values) {
                if (!indexOf.TryGetValue(v, out var index)) {
                    index = dictionary.Count;
                    dictionary.Add(v);
                    indexOf[v] = index;
                }
                items.Add(index);
            }
        }
        offsets[values.Length] = items.Count;
        column.ManyOffsets = offsets;
        column.ManyItems = [.. items];
        column.Dictionary = [.. dictionary];
    }

    private static string? TextOf(FilterSubjectValue value) => value switch {
        FilterSubjectValue.TextValue t => t.Value,
        FilterSubjectValue.NumberValue n => n.Value.ToString(CultureInfo.InvariantCulture),
        FilterSubjectValue.BoolValue b => b.Value ? "true" : "false",
        FilterSubjectValue.DateValue d => d.Value.ToString("O", CultureInfo.InvariantCulture),
        _ => null,
    };
}
