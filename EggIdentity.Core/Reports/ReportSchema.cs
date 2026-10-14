using EggIdentity.Filters;

namespace EggIdentity.Reports;

public sealed record ReportDimension(FilterFieldDef Field) {
    public DimensionOrder Order { get; init; } = DimensionOrder.Natural;
    public IReadOnlyList<TimeBucket> Buckets { get; init; } = Field.Kind == FilterValueKind.Date ? TimeBuckets.All : [];

    public string Key => Field.Key;

    public string Label(string key) {
        ArgumentNullException.ThrowIfNull(key);
        return Option(key)?.Label ?? key;
    }

    public FilterOption? Option(string key) =>
        Field.Options.FirstOrDefault(o => string.Equals(o.Value, key, StringComparison.Ordinal));

    public int Compare(string a, string b) {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return Order switch {
            DimensionOrder.CustomOrder custom => custom.Comparison(a, b),
            DimensionOrder.DeclaredOrder => Declared(a, b),
            _ => DimensionOrder.CompareNatural(a, b),
        };
    }

    private int Declared(string a, string b) {
        var ia = Index(a);
        var ib = Index(b);
        if (ia < 0 && ib < 0) return DimensionOrder.CompareNatural(a, b);
        if (ia < 0) return 1;
        if (ib < 0) return -1;
        return ia.CompareTo(ib);
    }

    private int Index(string key) {
        for (var i = 0; i < Field.Options.Count; i++)
            if (string.Equals(Field.Options[i].Value, key, StringComparison.Ordinal)) return i;
        return -1;
    }
}

public sealed record ReportMeasure(string Key, string Label) {
    public MeasureKind Kind { get; init; } = MeasureKind.Scalar;
    public IReadOnlyList<Aggregation> Aggregations { get; init; } = [Aggregation.Sum, Aggregation.Avg, Aggregation.Min, Aggregation.Max];
    public Aggregation Default { get; init; } = Aggregation.Sum;
    public ValueFormat Format { get; init; } = ValueFormat.Decimal(2);
    public double? Binning { get; init; }
    public string? DistinctBy { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = [];

    public bool Allows(string? scope) =>
        scope is null || Scopes.Count == 0 || Scopes.Contains(scope, StringComparer.Ordinal);
}

public interface IReportSource {
    string Id { get; }
    string Label { get; }
    IReadOnlyList<ReportDimension> Dimensions { get; }
    IReadOnlyList<ReportMeasure> Measures { get; }
    FilterCatalog Catalog { get; }
    ReportDimension? Dimension(string key);
    ReportMeasure? Measure(string key);
}

public sealed record ReportSource<TRow>(
    string Id,
    string Label,
    IReadOnlyList<ReportDimension> Dimensions,
    IReadOnlyList<ReportMeasure> Measures,
    Func<TRow, string, FilterSubjectValue> Value) : IReportSource {
    public string? SampleKey { get; init; }
    public string? Scope { get; init; }
    public FilterSet Defaults { get; init; } = FilterSet.Empty;

    public FilterCatalog Catalog => new(Id, [.. Dimensions.Select(d => d.Field)], Defaults);

    public ReportDimension? Dimension(string key) => Dimensions.FirstOrDefault(d => d.Field.Answers(key));

    public ReportMeasure? Measure(string key) =>
        Measures.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.Ordinal));
}
