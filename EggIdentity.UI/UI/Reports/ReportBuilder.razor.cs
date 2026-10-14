using System.Globalization;
using EggIdentity.Filters;
using EggIdentity.Reports;
using Microsoft.AspNetCore.Components;
using SortKey = EggIdentity.Reports.SortKey;

namespace EggIdentity.UI.Reports;

public sealed partial class ReportBuilder : ComponentBase {
    [Parameter, EditorRequired] public IReadOnlyList<IReportSource> Sources { get; set; } = [];
    [Parameter, EditorRequired] public ReportDefinition Value { get; set; } = Empty;
    [Parameter] public EventCallback<ReportDefinition> ValueChanged { get; set; }
    [Parameter] public IReadOnlyList<string> Charts { get; set; } = ReportCharts.BuiltIn;
    [Parameter] public IReportCostEstimator? Cost { get; set; }
    [Parameter] public ReportBuilderLabels Labels { get; set; } = ReportBuilderLabels.Default;
    [Parameter] public ReportTier Tier { get; set; } = ReportTier.Live;
    [Parameter] public ReportEngineRegistry? Registry { get; set; }

    private static ReportDefinition Empty { get; } = new(ReportCodec.CurrentVersion, "", "", new ReportQuery(""), new ReportView(ReportCharts.Table));

    private ReportQuery Query => Value.Query;
    private ReportView View => Value.View;
    private IReportSource? Source => Sources.FirstOrDefault(s => string.Equals(s.Id, Query.Source, StringComparison.Ordinal));
    private ReportCost? EstimatedCost => Cost?.Estimate(Query);
    private bool Gated => Tier == ReportTier.Snapshot && Registry is not null;

    private IReadOnlyList<string> SortKeyOptions =>
        Source is null ? [] : [.. Source.Dimensions.Select(d => d.Key), .. Source.Measures.Select(m => m.Key), MeasureRequest.RowCountKey];

    private IReadOnlyList<FilterOption> LabelColorOptions {
        get {
            if (Head(Query.GroupBy) is not { } first) return [];
            var dim = Source?.Dimension(first.Dimension);
            return dim?.Field.Options ?? [];
        }
    }

    private string NormalizeKindValue => Query.Normalize.Kind switch {
        NormalizeKind.Row => "row",
        NormalizeKind.Column => "column",
        NormalizeKind.Global => "global",
        NormalizeKind.By => "by",
        _ => "none",
    };

    private static T? Head<T>(IReadOnlyList<T>? list) where T : class => list is { Count: > 0 } ? list[0] : null;

    private string? Reason(ReportQuery probe) {
        if (!Gated) return null;
        var validation = Registry!.Validate(probe, Tier);
        return validation.Allowed ? null : string.Join(" ", validation.Rejections.Select(r => r.Message));
    }

    private string? DimensionReason(string key) =>
        Reason(Query with { GroupBy = [new GroupKey(key)], Measures = [], Filters = FilterSet.Empty, Normalize = Normalize.None, Sort = [], Having = [] });

    private string? BucketReason(int index, TimeBucket bucket) {
        var groupBy = Query.GroupBy.ToList();
        groupBy[index] = groupBy[index] with { Bucket = bucket };
        return Reason(Query with { GroupBy = groupBy, Measures = [], Filters = FilterSet.Empty, Normalize = Normalize.None, Sort = [], Having = [] });
    }

    private string? MeasureReason(string key, Aggregation aggregation) =>
        Reason(Query with { GroupBy = [], Measures = [new MeasureRequest(key, aggregation)], Filters = FilterSet.Empty, Normalize = Normalize.None, Sort = [], Having = [] });

    private string? QueryReason => Reason(Query);

    private IReadOnlyList<FilterFieldDef> FilterFields {
        get {
            if (Source is not { } source) return [];
            if (!Gated) return source.Catalog.Fields;
            return [.. source.Catalog.Fields.Where(f => Reason(Query with {
                GroupBy = [],
                Measures = [],
                Normalize = Normalize.None,
                Sort = [],
                Having = [],
                Filters = new FilterSet([new FilterCondition(f.Key, "=", "x")], []),
            }) is null)];
        }
    }

    private Task Emit(ReportDefinition next) => ValueChanged.InvokeAsync(next);

    private Task SetTitle(string? value) => Emit(Value with { Title = value ?? "" });

    private Task SetDescription(string? value) => Emit(Value with { Description = string.IsNullOrEmpty(value) ? null : value });

    private Task SetSourceAsync(string? id) {
        if (string.Equals(id, Query.Source, StringComparison.Ordinal)) return Task.CompletedTask;
        var next = Sources.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
        var groupBy = next is null ? [] : Query.GroupBy.Where(g => next.Dimension(g.Dimension) is not null).ToList();
        var measures = next is null ? [] : Query.Measures.Where(m => m.IsRowCount || next.Measure(m.Key) is not null).ToList();
        var sort = next is null ? [] : Query.Sort.Where(s => next.Dimension(s.Key) is not null || next.Measure(s.Key) is not null).ToList();
        var having = next is null ? [] : Query.Having.Where(h => next.Measure(h.MeasureKey) is not null).ToList();
        var normalize = next is not null && Query.Normalize.Kind == NormalizeKind.By && next.Measure(Query.Normalize.Measure ?? "") is null
            ? Normalize.None
            : Query.Normalize;
        var query = Query with {
            Source = id ?? "",
            GroupBy = groupBy,
            Measures = measures,
            Filters = FilterSet.Empty,
            Sort = sort,
            Having = having,
            Normalize = normalize,
        };
        return Emit(Value with { Query = query });
    }

    private Task AddGroupBy() {
        var used = Query.GroupBy.Select(g => g.Dimension).ToHashSet(StringComparer.Ordinal);
        var dim = Source?.Dimensions.FirstOrDefault(d => !used.Contains(d.Key)) ?? Head(Source?.Dimensions);
        return Emit(Value with { Query = Query with { GroupBy = [.. Query.GroupBy, new GroupKey(dim?.Key ?? "")] } });
    }

    private Task RemoveGroupBy(int index) =>
        Emit(Value with { Query = Query with { GroupBy = Query.GroupBy.Where((_, i) => i != index).ToList() } });

    private Task SetGroupByDimension(int index, string? key) {
        var dim = Source?.Dimension(key ?? "");
        var bucket = dim is { Buckets.Count: > 0 } ? dim.Buckets[0] : (TimeBucket?)null;
        return UpdateGroupBy(index, _ => new GroupKey(key ?? "", bucket));
    }

    private Task SetGroupByBucket(int index, TimeBucket? bucket) => UpdateGroupBy(index, g => g with { Bucket = bucket });

    private Task SetGroupByFillGaps(int index, bool fill) => UpdateGroupBy(index, g => g with { FillGaps = fill });

    private Task UpdateGroupBy(int index, Func<GroupKey, GroupKey> update) {
        var list = Query.GroupBy.ToList();
        list[index] = update(list[index]);
        return Emit(Value with { Query = Query with { GroupBy = list } });
    }

    private Task AddMeasure() {
        var measure = Head(Source?.Measures);
        var request = measure is null ? MeasureRequest.RowCount : new MeasureRequest(measure.Key, measure.Default);
        return Emit(Value with { Query = Query with { Measures = [.. Query.Measures, request] } });
    }

    private Task RemoveMeasure(int index) =>
        Emit(Value with { Query = Query with { Measures = Query.Measures.Where((_, i) => i != index).ToList() } });

    private Task SetMeasureKey(int index, string? key) {
        if (string.IsNullOrEmpty(key) || string.Equals(key, MeasureRequest.RowCountKey, StringComparison.Ordinal))
            return UpdateMeasure(index, _ => MeasureRequest.RowCount);
        var measure = Source?.Measure(key);
        return UpdateMeasure(index, _ => new MeasureRequest(key, measure?.Default ?? Aggregation.Sum));
    }

    private Task SetMeasureAggregation(int index, Aggregation aggregation) => UpdateMeasure(index, m => m with { Aggregation = aggregation });

    private Task UpdateMeasure(int index, Func<MeasureRequest, MeasureRequest> update) {
        var list = Query.Measures.ToList();
        list[index] = update(list[index]);
        return Emit(Value with { Query = Query with { Measures = list } });
    }

    private Task SetFilters(FilterSet set) => Emit(Value with { Query = Query with { Filters = set } });

    private Task SetNormalizeKind(string? kind) {
        if (string.Equals(kind, "by", StringComparison.Ordinal)) {
            var measure = Query.Measures.FirstOrDefault(m => !m.IsRowCount)?.Key ?? Head(Source?.Measures)?.Key;
            return Emit(Value with { Query = Query with { Normalize = measure is null ? Normalize.None : Normalize.By(measure) } });
        }
        var normalize = kind switch {
            "row" => Normalize.Row,
            "column" => Normalize.Column,
            "global" => Normalize.Global,
            _ => Normalize.None,
        };
        return Emit(Value with { Query = Query with { Normalize = normalize } });
    }

    private Task SetNormalizeByMeasure(string? key) =>
        Emit(Value with { Query = Query with { Normalize = string.IsNullOrEmpty(key) ? Normalize.None : Normalize.By(key) } });

    private bool NormalizeByDisabled(ReportMeasure measure) {
        foreach (var g in Query.GroupBy) {
            var dim = Source?.Dimension(g.Dimension);
            if (dim is not null && !measure.Allows(dim.Field.Scope)) return true;
        }
        return false;
    }

    private Task AddSort() => Emit(Value with { Query = Query with { Sort = [.. Query.Sort, new SortKey(Head(SortKeyOptions) ?? "")] } });

    private Task RemoveSort(int index) =>
        Emit(Value with { Query = Query with { Sort = Query.Sort.Where((_, i) => i != index).ToList() } });

    private Task SetSortKey(int index, string? key) => UpdateSort(index, s => s with { Key = key ?? "" });

    private Task SetSortDescending(int index, bool descending) => UpdateSort(index, s => s with { Descending = descending });

    private Task UpdateSort(int index, Func<SortKey, SortKey> update) {
        var list = Query.Sort.ToList();
        list[index] = update(list[index]);
        return Emit(Value with { Query = Query with { Sort = list } });
    }

    private Task SetLimit(int? limit) => Emit(Value with { Query = Query with { Limit = limit } });

    private Task AddHaving() {
        var key = Head(Source?.Measures)?.Key ?? MeasureRequest.RowCountKey;
        return Emit(Value with { Query = Query with { Having = [.. Query.Having, new HavingClause(key, FilterOps.Eq, 0)] } });
    }

    private Task RemoveHaving(int index) =>
        Emit(Value with { Query = Query with { Having = Query.Having.Where((_, i) => i != index).ToList() } });

    private Task SetHavingMeasure(int index, string? key) => UpdateHaving(index, h => h with { MeasureKey = key ?? "" });

    private Task SetHavingOp(int index, string? op) => UpdateHaving(index, h => h with { Op = op ?? FilterOps.Eq });

    private Task SetHavingThreshold(int index, double threshold) => UpdateHaving(index, h => h with { Threshold = threshold });

    private Task UpdateHaving(int index, Func<HavingClause, HavingClause> update) {
        var list = Query.Having.ToList();
        list[index] = update(list[index]);
        return Emit(Value with { Query = Query with { Having = list } });
    }

    private Task SetMinSample(int? min) => Emit(Value with { Query = Query with { MinSample = min } });

    private Task SetSampleRule(SampleRule rule) => Emit(Value with { View = View with { Sample = rule } });

    private Task SetChart(string? chart) => Emit(Value with { View = View with { Chart = chart ?? ReportCharts.Table } });

    private Task SetColor(string? hex) => Emit(Value with { View = View with { Color = string.IsNullOrEmpty(hex) ? null : hex } });

    private Task SetUnfilled(string? hex) => Emit(Value with { View = View with { Unfilled = string.IsNullOrEmpty(hex) ? null : hex } });

    private Task SetLabelColor(string value, string? hex) {
        var map = new Dictionary<string, string>(View.LabelColors, StringComparer.Ordinal);
        if (string.IsNullOrEmpty(hex)) map.Remove(value);
        else map[value] = hex;
        return Emit(Value with { View = View with { LabelColors = map } });
    }

    private string LabelColorOr(string value) =>
        View.LabelColors.TryGetValue(value, out var hex) && ReportCodec.IsHex(hex) ? hex
        : ReportCodec.IsHex(View.Color) ? View.Color! : "#6366f1";

    private static string CostClass(ReportCostLevel level) => level switch {
        ReportCostLevel.Low => "report-cost-low",
        ReportCostLevel.Medium => "report-cost-medium",
        _ => "report-cost-high",
    };

    private static TimeBucket? ParseBucket(string? value) => Enum.TryParse<TimeBucket>(value, out var bucket) ? bucket : null;

    private static Aggregation ParseAggregation(string? value) => Enum.TryParse<Aggregation>(value, out var aggregation) ? aggregation : Aggregation.Sum;

    private static SampleRule ParseSampleRule(string? value) => Enum.TryParse<SampleRule>(value, out var rule) ? rule : SampleRule.Show;

    private static int? ParseInt(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static double ParseDouble(string? value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
