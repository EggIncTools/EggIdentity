using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EggIdentity.Filters;

namespace EggIdentity.Reports;

internal sealed class TimeBucketConverter : JsonStringEnumConverter<TimeBucket> {
    public TimeBucketConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

internal sealed class AggregationConverter : JsonStringEnumConverter<Aggregation> {
    public AggregationConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

internal sealed class MeasureKindConverter : JsonStringEnumConverter<MeasureKind> {
    public MeasureKindConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

internal sealed class NormalizeKindConverter : JsonStringEnumConverter<NormalizeKind> {
    public NormalizeKindConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

internal sealed class SampleRuleConverter : JsonStringEnumConverter<SampleRule> {
    public SampleRuleConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

internal sealed class HeatScaleConverter : JsonStringEnumConverter<HeatScale> {
    public HeatScaleConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

internal sealed class ReportTierConverter : JsonStringEnumConverter<ReportTier> {
    public ReportTierConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

internal sealed class ReportRejectionKindConverter : JsonStringEnumConverter<ReportRejectionKind> {
    public ReportRejectionKindConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

internal sealed class FilterValueKindConverter : JsonStringEnumConverter<FilterValueKind> {
    public FilterValueKindConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

internal sealed class ValueFormatKindConverter : JsonStringEnumConverter<ValueFormatKind> {
    public ValueFormatKindConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = false,
    Converters = [
        typeof(FilterCodec.ConditionConverter), typeof(FilterCodec.SetConverter),
        typeof(TimeBucketConverter), typeof(AggregationConverter), typeof(MeasureKindConverter), typeof(NormalizeKindConverter),
        typeof(SampleRuleConverter), typeof(HeatScaleConverter), typeof(ReportTierConverter), typeof(ReportRejectionKindConverter),
        typeof(FilterValueKindConverter), typeof(ValueFormatKindConverter),
    ])]
[JsonSerializable(typeof(FilterSet))]
[JsonSerializable(typeof(FilterCondition))]
[JsonSerializable(typeof(FilterCatalog))]
[JsonSerializable(typeof(FilterFieldDef))]
[JsonSerializable(typeof(FilterOpDef))]
[JsonSerializable(typeof(FilterOption))]
[JsonSerializable(typeof(ReportDefinition))]
[JsonSerializable(typeof(ReportPlacement))]
[JsonSerializable(typeof(ReportQuery))]
[JsonSerializable(typeof(ReportContext))]
[JsonSerializable(typeof(ReportCube))]
[JsonSerializable(typeof(ReportTierCaps))]
[JsonSerializable(typeof(ReportRejection[]))]
[JsonSerializable(typeof(SnapshotSchema))]
[JsonSerializable(typeof(CubeWire))]
[JsonSerializable(typeof(SourceDescriptor))]
[JsonSerializable(typeof(DimensionDescriptor))]
[JsonSerializable(typeof(MeasureDescriptor))]
[JsonSerializable(typeof(ReportPreset))]
[JsonSerializable(typeof(Dictionary<string, ReportTierCaps>))]
[JsonSerializable(typeof(JsonObject))]
[JsonSerializable(typeof(GroupKey))]
[JsonSerializable(typeof(MeasureRequest))]
[JsonSerializable(typeof(Normalize))]
[JsonSerializable(typeof(SortKey))]
[JsonSerializable(typeof(HavingClause))]
[JsonSerializable(typeof(ReportView))]
[JsonSerializable(typeof(HeatView))]
[JsonSerializable(typeof(CubeRow))]
[JsonSerializable(typeof(Distribution))]
[JsonSerializable(typeof(DistributionBucket))]
internal sealed partial class ReportJsonContext : JsonSerializerContext {
}
