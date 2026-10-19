using EggIdentity.Filters;

namespace EggIdentity.Reports;

public static class SnapshotSource {
    public static ReportSource<int> Create(SourceDescriptor descriptor, RowSnapshot snapshot) {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(snapshot.Source, descriptor.Id, StringComparison.Ordinal))
            throw new ArgumentException($"snapshot source \"{snapshot.Source}\" does not match descriptor \"{descriptor.Id}\"", nameof(snapshot));

        var dimensions = descriptor.Dimensions.Select(d => BuildDimension(descriptor, d)).ToList();
        var measures = descriptor.Measures.Select(BuildMeasure).ToList();

        return new ReportSource<int>(descriptor.Id, descriptor.Label, dimensions, measures, (row, key) => snapshot.Column(key)?.At(row) ?? FilterSubjectValue.Absent) {
            SampleKey = descriptor.SampleKey,
            Scope = descriptor.Scope,
            Defaults = descriptor.Defaults,
            Presets = descriptor.Presets,
        };
    }

    private static ReportDimension BuildDimension(SourceDescriptor descriptor, DimensionDescriptor dimension) {
        var field = descriptor.Fields.First(f => f.Answers(dimension.Field));
        var order = dimension.CustomOrder is { } custom
            ? DimensionOrder.Custom(Comparison(custom))
            : dimension.DeclaredOrder ? DimensionOrder.OptionOrder : DimensionOrder.Natural;
        return new ReportDimension(field) {
            Order = order,
            Buckets = dimension.Buckets,
        };
    }

    private static Comparison<string> Comparison(IReadOnlyList<string> order) {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < order.Count; i++) index[order[i]] = i;
        return (a, b) => {
            var ia = index.TryGetValue(a, out var x) ? x : -1;
            var ib = index.TryGetValue(b, out var y) ? y : -1;
            if (ia < 0 && ib < 0) return DimensionOrder.CompareNatural(a, b);
            if (ia < 0) return 1;
            if (ib < 0) return -1;
            return ia.CompareTo(ib);
        };
    }

    private static ReportMeasure BuildMeasure(MeasureDescriptor measure) => new(measure.Key, measure.Label) {
        Kind = measure.Kind,
        Aggregations = measure.Aggregations,
        Default = measure.Default,
        Format = measure.Format switch {
            ValueFormatKind.Integer => ValueFormat.Integer,
            ValueFormatKind.Percent => ValueFormat.Percent,
            ValueFormatKind.Duration => ValueFormat.Duration,
            _ => ValueFormat.Decimal(measure.Places),
        },
        Binning = measure.Binning,
        DistinctBy = measure.DistinctBy,
        Scopes = measure.Scopes,
    };
}
