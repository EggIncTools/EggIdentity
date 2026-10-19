using EggIdentity.Filters;

namespace EggIdentity.Reports;

public sealed record DimensionDescriptor(string Field, bool DeclaredOrder, IReadOnlyList<string>? CustomOrder, IReadOnlyList<TimeBucket> Buckets);

public sealed record MeasureDescriptor(
    string Key, string Label, MeasureKind Kind, IReadOnlyList<Aggregation> Aggregations, Aggregation Default,
    ValueFormatKind Format, int Places, double? Binning, string? DistinctBy, IReadOnlyList<string> Scopes);

public sealed record SourceDescriptor(
    string Id, string Label, IReadOnlyList<FilterFieldDef> Fields, IReadOnlyList<DimensionDescriptor> Dimensions,
    IReadOnlyList<MeasureDescriptor> Measures, string? SampleKey, string? Scope, FilterSet Defaults, IReadOnlyList<ReportPreset> Presets) {
    public static (SourceDescriptor Descriptor, IReadOnlyList<string> Downgrades) Capture(IReportSource source) {
        ArgumentNullException.ThrowIfNull(source);
        var downgrades = new List<string>();

        var dimensions = new List<DimensionDescriptor>(source.Dimensions.Count);
        foreach (var dimension in source.Dimensions) {
            IReadOnlyList<string>? customOrder = null;
            if (dimension.Order is DimensionOrder.CustomOrder custom) {
                if (dimension.Field.Options.Count > 0) {
                    var values = dimension.Field.Options.Select(o => o.Value).ToList();
                    values.Sort(custom.Comparison);
                    customOrder = values;
                } else {
                    downgrades.Add(dimension.Key);
                }
            }
            dimensions.Add(new DimensionDescriptor(dimension.Key, dimension.Order is DimensionOrder.DeclaredOrder, customOrder, dimension.Buckets));
        }

        var measures = new List<MeasureDescriptor>(source.Measures.Count);
        foreach (var measure in source.Measures) {
            var format = measure.Format;
            ValueFormatKind kind;
            int places;
            if (format.Kind == ValueFormatKind.Custom) {
                downgrades.Add(measure.Key);
                kind = ValueFormatKind.Decimal;
                places = 2;
            } else {
                kind = (ValueFormatKind)format.Kind;
                places = format.Places;
            }
            measures.Add(new MeasureDescriptor(
                measure.Key, measure.Label, measure.Kind, measure.Aggregations, measure.Default, kind, places, measure.Binning, measure.DistinctBy, measure.Scopes));
        }

        var descriptor = new SourceDescriptor(
            source.Id, source.Label, source.Catalog.Fields, dimensions, measures, source.SampleKey, source.Scope, source.Catalog.Defaults, source.Presets);
        return (descriptor, downgrades);
    }
}
