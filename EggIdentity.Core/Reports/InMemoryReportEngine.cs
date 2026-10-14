using EggIdentity.Filters;

namespace EggIdentity.Reports;

public sealed class InMemoryReportEngine<TRow>(
    ReportSource<TRow> source, Func<CancellationToken, ValueTask<IReadOnlyList<TRow>>> rows, TimeProvider clock) : IReportEngine {
    private const char Separator = '\u001f';

    public async Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(query.Source, source.Id, StringComparison.Ordinal))
            throw new ArgumentException($"query source \"{query.Source}\" does not match \"{source.Id}\"", nameof(query));
        var now = clock.GetUtcNow();
        var dims = query.GroupBy.Select(Dimension).ToList();
        var measures = query.EffectiveMeasures;
        var defs = measures.Select(m => m.IsRowCount ? null : Measure(m.Key)).ToList();
        var groups = new Dictionary<string, Group>(StringComparer.Ordinal);
        var evaluation = new FilterEvaluation(source.Scope, Now: now);
        var fields = source.Catalog.Fields;

        foreach (var row in await rows(ct).ConfigureAwait(false)) {
            ct.ThrowIfCancellationRequested();
            FilterSubjectValue Value(string key) {
                return source.Value(row, key);
            }
            if (!FilterEvaluator.Evaluate(query.Filters, fields, Value, evaluation).Matched) continue;
            foreach (var keys in Combinations(query.GroupBy, dims, Value, context.Zone)) {
                var id = Id(keys);
                if (!groups.TryGetValue(id, out var group)) {
                    group = new Group(keys, [.. measures.Select((m, i) => new MeasureAccumulator(m, defs[i]))], Denominator(query.Normalize), source.SampleKey);
                    groups[id] = group;
                }
                group.Add(Value);
            }
        }

        var cubeRows = Fill([.. groups.Values.Select(g => g.ToRow())], query, dims, measures, context.Zone, now);
        cubeRows = [.. cubeRows.Where(r => CubeShaping.Passes(r, query.Having, measures))];
        var allowed = NormalizeAllowed(query, dims);
        var normalized = CubeShaping.Normalize(cubeRows, query.Normalize, allowed, keys => groups.GetValueOrDefault(Id(keys))?.DenominatorValue);
        var sorted = CubeShaping.Sort(normalized, query.Sort, dims, measures);
        var limited = CubeShaping.Limit(sorted, query.Limit, query.Sort, measures);
        var applied = query.Normalize.Kind != NormalizeKind.None && allowed;
        return new ReportCube([.. dims.Select(d => d.Key)], measures, applied, limited);
    }

    private static string Id(IEnumerable<string> keys) => string.Join(Separator, keys);

    private ReportDimension Dimension(GroupKey key) =>
        source.Dimension(key.Dimension) ?? throw new ArgumentException($"unknown dimension \"{key.Dimension}\"", nameof(key));

    private ReportMeasure Measure(string key) =>
        source.Measure(key) ?? throw new ArgumentException($"unknown measure \"{key}\"", nameof(key));

    private MeasureAccumulator? Denominator(Normalize normalize) {
        if (normalize.Kind != NormalizeKind.By || normalize.Measure is not { } key) return null;
        if (string.Equals(key, MeasureRequest.RowCountKey, StringComparison.Ordinal)) return new MeasureAccumulator(MeasureRequest.RowCount, null);
        var measure = Measure(key);
        return new MeasureAccumulator(new MeasureRequest(key, measure.Default), measure);
    }

    private bool NormalizeAllowed(ReportQuery query, List<ReportDimension> dims) {
        if (query.Normalize.Kind != NormalizeKind.By || query.Normalize.Measure is not { } key) return true;
        if (string.Equals(key, MeasureRequest.RowCountKey, StringComparison.Ordinal)) return true;
        var measure = Measure(key);
        return dims.TrueForAll(d => measure.Allows(d.Field.Scope));
    }

    private static List<IReadOnlyList<string>> Combinations(
        IReadOnlyList<GroupKey> groupBy, List<ReportDimension> dims, Func<string, FilterSubjectValue> value, TimeZoneInfo zone) {
        List<IReadOnlyList<string>> acc = [[]];
        for (var axis = 0; axis < groupBy.Count; axis++) {
            var options = ReportValues.GroupKeys(value(dims[axis].Key), groupBy[axis].Bucket, zone);
            acc = [.. acc.SelectMany(prefix => options.Select(o => Append(prefix, o)))];
        }
        return acc;
    }

    private static List<CubeRow> Fill(
        List<CubeRow> cubeRows, ReportQuery query, List<ReportDimension> dims, IReadOnlyList<MeasureRequest> measures, TimeZoneInfo zone, DateTimeOffset now) {
        for (var axis = 0; axis < query.GroupBy.Count; axis++) {
            if (query.GroupBy[axis] is not { Bucket: { } bucket, FillGaps: true }) continue;
            var position = axis;
            var full = GapFill.Keys(cubeRows.Select(r => r.Keys[position]), dims[axis], bucket, query.Filters, zone, now);
            FillAxis(cubeRows, position, full, dims.Count, measures);
        }
        return cubeRows;
    }

    private static void FillAxis(List<CubeRow> cubeRows, int position, IReadOnlyList<string> full, int arity, IReadOnlyList<MeasureRequest> measures) {
        if (full.Count == 0) return;
        var present = cubeRows.Select(r => Id(r.Keys)).ToHashSet(StringComparer.Ordinal);
        var templates = cubeRows.Select(r => r.Keys).DistinctBy(k => Id(k.Where((_, i) => i != position)), StringComparer.Ordinal).ToList();
        if (templates.Count == 0 && arity == 1) templates = [[""]];
        var added = templates
            .SelectMany(template => full.Select(key => Replace(template, position, key)))
            .Where(keys => present.Add(Id(keys)))
            .Select(keys => Empty(keys, measures))
            .ToList();
        cubeRows.AddRange(added);
    }

    private static IReadOnlyList<string> Append(IReadOnlyList<string> prefix, string key) => [.. prefix, key];

    private static IReadOnlyList<string> Replace(IReadOnlyList<string> template, int position, string key) =>
        [.. template.Select((k, i) => i == position ? key : k)];

    private static CubeRow Empty(IReadOnlyList<string> keys, IReadOnlyList<MeasureRequest> measures) =>
        new(keys, [.. measures.Select(m => MeasureAccumulator.Empty(m.Aggregation))], 0) {
            Distributions = [.. measures.Select(_ => (Distribution?)null)],
        };

    private sealed class Group(IReadOnlyList<string> keys, IReadOnlyList<MeasureAccumulator> accumulators, MeasureAccumulator? denominator, string? sampleKey) {
        private readonly HashSet<string> _samples = [];
        private int _rows;

        public double? DenominatorValue => denominator?.Result();

        public void Add(Func<string, FilterSubjectValue> value) {
            _rows++;
            if (sampleKey is not null && ReportValues.First(value(sampleKey)) is { } sample) _samples.Add(sample);
            foreach (var a in accumulators) a.Add(value);
            denominator?.Add(value);
        }

        public CubeRow ToRow() => new(keys, [.. accumulators.Select(a => a.Result())], sampleKey is null ? _rows : _samples.Count) {
            Distributions = [.. accumulators.Select(a => a.Distribution())],
        };
    }
}
