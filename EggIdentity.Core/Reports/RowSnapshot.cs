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
            var kind = source.Dimension(key) is { } dimension ? ColumnKind(dimension.Field.Kind) : InferKind(source, rows, key);
            columns.Add(BuildColumn(key, kind, rows, row => source.Value(row, key)));
        }

        return new RowSnapshot(source.Id, version, rows.Count, columns);
    }

    private static FilterValueKind InferKind<TRow>(ReportSource<TRow> source, IReadOnlyList<TRow> rows, string key) {
        for (var r = 0; r < rows.Count; r++) {
            switch (source.Value(rows[r], key)) {
                case FilterSubjectValue.NumberValue: return FilterValueKind.Number;
                case FilterSubjectValue.TextValue: return FilterValueKind.Text;
                case FilterSubjectValue.BoolValue: return FilterValueKind.Bool;
                case FilterSubjectValue.DateValue: return FilterValueKind.Date;
                case FilterSubjectValue.ManyValue: return FilterValueKind.Set;
            }
        }
        return FilterValueKind.Number;
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

    private static SnapshotColumn BuildColumn<TRow>(string key, FilterValueKind kind, IReadOnlyList<TRow> rows, Func<TRow, FilterSubjectValue> value) {
        var column = new SnapshotColumn(key, kind, rows.Count);
        switch (kind) {
            case FilterValueKind.Number:
                BuildNumbers(column, rows, value);
                break;
            case FilterValueKind.Date:
                BuildDates(column, rows, value);
                break;
            case FilterValueKind.Bool:
                BuildBools(column, rows, value);
                break;
            case FilterValueKind.Set:
                BuildMany(column, rows, value);
                break;
            default:
                BuildText(column, rows, value);
                break;
        }
        return column;
    }

    private static void BuildNumbers<TRow>(SnapshotColumn column, IReadOnlyList<TRow> rows, Func<TRow, FilterSubjectValue> value) {
        var numbers = new double[rows.Count];
        for (var i = 0; i < rows.Count; i++) {
            if (value(rows[i]) is not FilterSubjectValue.NumberValue n) continue;
            column.Mark(i);
            numbers[i] = n.Value;
        }
        column.Numbers = numbers;
    }

    private static void BuildDates<TRow>(SnapshotColumn column, IReadOnlyList<TRow> rows, Func<TRow, FilterSubjectValue> value) {
        var dates = new long[rows.Count];
        for (var i = 0; i < rows.Count; i++) {
            if (value(rows[i]) is not FilterSubjectValue.DateValue d) continue;
            column.Mark(i);
            dates[i] = d.Value.ToUnixTimeSeconds();
        }
        column.Dates = dates;
    }

    private static void BuildBools<TRow>(SnapshotColumn column, IReadOnlyList<TRow> rows, Func<TRow, FilterSubjectValue> value) {
        var bools = new bool[rows.Count];
        for (var i = 0; i < rows.Count; i++) {
            if (value(rows[i]) is not FilterSubjectValue.BoolValue b) continue;
            column.Mark(i);
            bools[i] = b.Value;
        }
        column.Bools = bools;
    }

    private static void BuildText<TRow>(SnapshotColumn column, IReadOnlyList<TRow> rows, Func<TRow, FilterSubjectValue> value) {
        var dictionary = new List<string>();
        var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var text = new int[rows.Count];
        for (var i = 0; i < rows.Count; i++) {
            if (TextOf(value(rows[i])) is not { } s) continue;
            column.Mark(i);
            text[i] = Intern(dictionary, indexOf, s);
        }
        column.Text = text;
        column.Dictionary = [.. dictionary];
    }

    private static void BuildMany<TRow>(SnapshotColumn column, IReadOnlyList<TRow> rows, Func<TRow, FilterSubjectValue> value) {
        var dictionary = new List<string>();
        var indexOf = new Dictionary<string, int>(StringComparer.Ordinal);
        var offsets = new int[rows.Count + 1];
        var items = new List<int>();
        for (var i = 0; i < rows.Count; i++) {
            offsets[i] = items.Count;
            if (value(rows[i]) is not FilterSubjectValue.ManyValue m) continue;
            column.Mark(i);
            foreach (var v in m.Values) items.Add(Intern(dictionary, indexOf, v));
        }
        offsets[rows.Count] = items.Count;
        column.ManyOffsets = offsets;
        column.ManyItems = [.. items];
        column.Dictionary = [.. dictionary];
    }

    private static int Intern(List<string> dictionary, Dictionary<string, int> indexOf, string s) {
        if (indexOf.TryGetValue(s, out var index)) return index;
        index = dictionary.Count;
        dictionary.Add(s);
        indexOf[s] = index;
        return index;
    }

    private static string? TextOf(FilterSubjectValue value) => value switch {
        FilterSubjectValue.TextValue t => t.Value,
        FilterSubjectValue.NumberValue n => n.Value.ToString(CultureInfo.InvariantCulture),
        FilterSubjectValue.BoolValue b => b.Value ? "true" : "false",
        FilterSubjectValue.DateValue d => d.Value.ToString("O", CultureInfo.InvariantCulture),
        _ => null,
    };
}
