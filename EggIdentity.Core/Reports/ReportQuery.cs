using System.Text.Json.Serialization;
using EggIdentity.Filters;

namespace EggIdentity.Reports;

public sealed record GroupKey(string Dimension, TimeBucket? Bucket = null, bool FillGaps = true);

public sealed record MeasureRequest(string Key, Aggregation Aggregation) {
    public const string RowCountKey = "count";

    public static MeasureRequest RowCount { get; } = new(RowCountKey, Aggregation.Count);

    [JsonIgnore]
    public bool IsRowCount => string.Equals(Key, RowCountKey, StringComparison.Ordinal);
}

public enum NormalizeKind {
    None,
    Row,
    Column,
    Global,
    By,
}

public sealed record Normalize(NormalizeKind Kind, string? Measure = null) {
    public static Normalize None { get; } = new(NormalizeKind.None);

    public static Normalize Row { get; } = new(NormalizeKind.Row);

    public static Normalize Column { get; } = new(NormalizeKind.Column);

    public static Normalize Global { get; } = new(NormalizeKind.Global);

    public static Normalize By(string measure) {
        ArgumentException.ThrowIfNullOrWhiteSpace(measure);
        return new Normalize(NormalizeKind.By, measure);
    }
}

public sealed record SortKey(string Key, bool Descending = false);

public sealed record HavingClause(string MeasureKey, string Op, double Threshold);

public sealed record ReportQuery(string Source) {
    public IReadOnlyList<GroupKey> GroupBy { get; init; } = [];
    public IReadOnlyList<MeasureRequest> Measures { get; init; } = [];
    public FilterSet Filters { get; init; } = FilterSet.Empty;
    public Normalize Normalize { get; init; } = Normalize.None;
    public IReadOnlyList<SortKey> Sort { get; init; } = [];
    public IReadOnlyList<HavingClause> Having { get; init; } = [];
    public int? Limit { get; init; }
    public int? MinSample { get; init; }

    [JsonIgnore]
    public IReadOnlyList<MeasureRequest> EffectiveMeasures => Measures.Count == 0 ? [MeasureRequest.RowCount] : Measures;
}

public sealed record ReportContext(TimeZoneInfo Zone) {
    public static ReportContext Utc { get; } = new(TimeZoneInfo.Utc);
}
