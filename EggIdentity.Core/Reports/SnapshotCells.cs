using EggIdentity.Filters;

namespace EggIdentity.Reports;

internal sealed class MeasureCellStat {
    public long Rows { get; set; }
    public double Sum { get; set; }
    public long NumberCount { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public Dictionary<double, int>? Distribution { get; set; }

    public void Add(double value) {
        NumberCount++;
        Sum += value;
        Min = Min is { } min ? Math.Min(min, value) : value;
        Max = Max is { } max ? Math.Max(max, value) : value;
    }

    public void Bin(double value, double? binning) {
        Distribution ??= [];
        var key = binning is { } b && b > 0 ? Math.Floor(value / b) * b : value;
        Distribution[key] = Distribution.GetValueOrDefault(key) + 1;
    }
}

internal sealed class SnapshotCell(IReadOnlyList<string> keys) {
    public IReadOnlyList<string> Keys { get; } = keys;
    public long Rows { get; private set; }
    public HashSet<string> Samples { get; } = [];
    public Dictionary<string, MeasureCellStat> Measures { get; } = [];
    public Dictionary<string, Dictionary<string, double?>> Entities { get; } = [];

    public void AddRow() => Rows++;

    public void AddMeasure(ReportMeasure measure, Func<string, FilterSubjectValue> value) {
        var number = value(measure.Key) is FilterSubjectValue.NumberValue n && double.IsFinite(n.Value) ? n.Value : (double?)null;
        if (measure.DistinctBy is { } by) {
            if (ReportValues.First(value(by)) is not { } entity) return;
            if (!Entities.TryGetValue(measure.Key, out var seen)) {
                seen = [];
                Entities[measure.Key] = seen;
            }
            seen.TryAdd(entity, number);
            return;
        }
        if (!Measures.TryGetValue(measure.Key, out var stat)) {
            stat = new MeasureCellStat();
            Measures[measure.Key] = stat;
        }
        stat.Rows++;
        if (number is { } v) {
            stat.Add(v);
            if (measure.Kind == MeasureKind.Distribution) stat.Bin(v, measure.Binning);
        }
    }
}

internal static class SnapshotCellBuilder {
    private const char Separator = '\u001f';

    public static IReadOnlyDictionary<string, SnapshotCell> Build<TRow>(
        IReadOnlyList<TRow> rows, ReportSource<TRow> source, SnapshotSchema schema, IReadOnlyList<ReportDimension> grainDims, TimeZoneInfo zone) {
        Dictionary<string, SnapshotCell> cells = [];
        foreach (var row in rows) {
            FilterSubjectValue Value(string key) {
                return source.Value(row, key);
            }
            foreach (var keys in Combinations(schema.Grain, grainDims, Value, zone)) {
                AddToCell(cells, keys, Value, source, schema);
            }
        }
        return cells;
    }

    private static void AddToCell<TRow>(
        Dictionary<string, SnapshotCell> cells, IReadOnlyList<string> keys, Func<string, FilterSubjectValue> value, ReportSource<TRow> source, SnapshotSchema schema) {
        var id = Id(keys);
        if (!cells.TryGetValue(id, out var cell)) {
            cell = new SnapshotCell(keys);
            cells[id] = cell;
        }
        cell.AddRow();
        if (source.SampleKey is { } sampleKey && ReportValues.First(value(sampleKey)) is { } sample) cell.Samples.Add(sample);
        foreach (var measureKey in schema.Measures) {
            if (source.Measure(measureKey) is { } measure) cell.AddMeasure(measure, value);
        }
    }

    private static List<IReadOnlyList<string>> Combinations(
        IReadOnlyList<GroupKey> grain, IReadOnlyList<ReportDimension> dims, Func<string, FilterSubjectValue> value, TimeZoneInfo zone) {
        List<IReadOnlyList<string>> acc = [[]];
        for (var axis = 0; axis < grain.Count; axis++) {
            var options = ReportValues.GroupKeys(value(dims[axis].Key), grain[axis].Bucket, zone);
            acc = [.. acc.SelectMany(prefix => options.Select(o => Append(prefix, o)))];
        }
        return acc;
    }

    private static IReadOnlyList<string> Append(IReadOnlyList<string> prefix, string key) => [.. prefix, key];

    private static string Id(IEnumerable<string> keys) => string.Join(Separator, keys);
}

internal sealed record SnapshotRollupResult(List<CubeRow> Rows, Dictionary<string, double?> DenominatorByTarget);

internal static class SnapshotRollup {
    private const char Separator = '\u001f';

    private readonly record struct AxisPlan(int GrainIndex, TimeBucket GrainBucket, TimeBucket? QueryBucket);

    public static SnapshotRollupResult Build<TRow>(
        IEnumerable<SnapshotCell> cells, ReportQuery query, ReportSource<TRow> source, SnapshotSchema schema,
        IReadOnlyList<ReportDimension> grainDims, IReadOnlyList<MeasureRequest> measures) {
        var plans = PlanAxes(query, grainDims, schema);
        var fan = FanPlan.Of(grainDims, plans, schema);
        var denominatorRequest = DenominatorRequest(source, query.Normalize);

        var byTarget = new Dictionary<string, (IReadOnlyList<string> Keys, List<SnapshotCell> Cells)>(StringComparer.Ordinal);
        foreach (var cell in cells) {
            var keys = TargetKeys(cell, plans);
            var id = Id(keys);
            if (!byTarget.TryGetValue(id, out var entry)) {
                entry = (keys, []);
                byTarget[id] = entry;
            }
            entry.Cells.Add(cell);
        }

        var rows = new List<CubeRow>();
        var denominators = new Dictionary<string, double?>(StringComparer.Ordinal);
        foreach (var (id, entry) in byTarget) {
            var samples = Samples(entry.Cells, source, fan);
            var values = new double?[measures.Count];
            var distributions = new Distribution?[measures.Count];
            for (var i = 0; i < measures.Count; i++) {
                var (value, dist) = Aggregate(entry.Cells, measures[i], source, grainDims, fan);
                values[i] = value;
                distributions[i] = dist;
            }
            rows.Add(new CubeRow(entry.Keys, values, samples) { Distributions = distributions });
            if (denominatorRequest is { } dr) {
                var (denomValue, _) = Aggregate(entry.Cells, dr, source, grainDims, fan);
                denominators[id] = denomValue;
            }
        }
        return new SnapshotRollupResult(rows, denominators);
    }

    private static List<AxisPlan> PlanAxes(ReportQuery query, IReadOnlyList<ReportDimension> grainDims, SnapshotSchema schema) {
        var plans = new List<AxisPlan>();
        foreach (var key in query.GroupBy) {
            var grainIndex = IndexOfGrainDim(grainDims, key.Dimension);
            var grainBucket = schema.Grain[grainIndex].Bucket ?? TimeBucket.Day;
            plans.Add(new AxisPlan(grainIndex, grainBucket, key.Bucket));
        }
        return plans;
    }

    private static int IndexOfGrainDim(IReadOnlyList<ReportDimension> grainDims, string key) {
        for (var i = 0; i < grainDims.Count; i++) {
            if (grainDims[i].Field.Answers(key)) return i;
        }
        throw new ArgumentException($"\"{key}\" is not a grain dimension", nameof(key));
    }

    private sealed record FanPlan(bool Declared, List<int> Other) {
        public static FanPlan Of(IReadOnlyList<ReportDimension> grainDims, List<AxisPlan> plans, SnapshotSchema schema) {
            if (schema.FanOut.Count == 0) return new FanPlan(false, []);
            var kept = plans.Select(p => p.GrainIndex).ToHashSet();
            List<int> other = [];
            for (var i = 0; i < grainDims.Count; i++) {
                if (!kept.Contains(i) && !schema.FanOut.Contains(grainDims[i].Field.Key, StringComparer.Ordinal)) other.Add(i);
            }
            return new FanPlan(true, other);
        }

        public double Merge(List<SnapshotCell> cells, Func<SnapshotCell, double> selector) =>
            cells.GroupBy(c => string.Join(Separator, Other.Select(i => c.Keys[i])), StringComparer.Ordinal).Sum(g => g.Max(selector));
    }

    private static int Samples<TRow>(List<SnapshotCell> cells, ReportSource<TRow> source, FanPlan fan) {
        if (source.SampleKey is null) return (int)cells.Sum(c => c.Rows);
        if (fan.Declared) return (int)fan.Merge(cells, c => c.Samples.Count);
        return cells.SelectMany(c => c.Samples).Distinct(StringComparer.Ordinal).Count();
    }

    private static double EntitySum(SnapshotCell cell, string measureKey, bool count) {
        if (!cell.Entities.TryGetValue(measureKey, out var entities)) return 0;
        return count ? entities.Count : entities.Values.Sum(v => v ?? 0);
    }

    private static double EntityNumbers(SnapshotCell cell, string measureKey) =>
        cell.Entities.TryGetValue(measureKey, out var entities) ? entities.Values.Count(v => v is not null) : 0;

    private static IReadOnlyList<string> TargetKeys(SnapshotCell cell, List<AxisPlan> plans) =>
        [.. plans.Select(p => Rekey(cell.Keys[p.GrainIndex], p.GrainBucket, p.QueryBucket))];

    private static string Rekey(string key, TimeBucket grainBucket, TimeBucket? queryBucket) {
        if (queryBucket is not { } qb || qb == grainBucket) return key;
        return TimeBuckets.Parse(key, grainBucket) is { } day ? TimeBuckets.Key(day, qb) : key;
    }

    private static double MergeMeasureSum(List<SnapshotCell> cells, string measureKey, Func<MeasureCellStat, double> selector) =>
        cells.Sum(c => MeasureValue(c, measureKey, selector));

    private static List<double?> EntityValues(List<SnapshotCell> cells, string measureKey) {
        Dictionary<string, double?> merged = [];
        foreach (var cell in cells) {
            if (!cell.Entities.TryGetValue(measureKey, out var entities)) continue;
            foreach (var (entity, value) in entities) merged.TryAdd(entity, value);
        }
        return [.. merged.Values];
    }

    private static (double? Value, Distribution? Dist) AggregateEntities(List<SnapshotCell> cells, ReportMeasure measure, Aggregation aggregation, FanPlan fan) {
        var values = EntityValues(cells, measure.Key);
        List<double> numbers = [.. values.OfType<double>()];
        var dist = measure.Kind == MeasureKind.Distribution ? Bucketize(numbers, measure.Binning) : null;
        if (fan.Declared && aggregation is Aggregation.Count or Aggregation.Sum or Aggregation.Avg) return (Fanned(cells, measure.Key, aggregation, fan), dist);
        double? value = aggregation switch {
            Aggregation.Count => values.Count,
            Aggregation.Sum => numbers.Sum(),
            Aggregation.Avg => numbers.Count == 0 ? null : numbers.Average(),
            Aggregation.Min => numbers.Count == 0 ? null : numbers.Min(),
            Aggregation.Max => numbers.Count == 0 ? null : numbers.Max(),
            Aggregation.Distinct => numbers.Distinct().Count(),
            _ => null,
        };
        return (value, dist);
    }

    private static double? Fanned(List<SnapshotCell> cells, string measureKey, Aggregation aggregation, FanPlan fan) {
        if (aggregation == Aggregation.Count) return fan.Merge(cells, c => EntitySum(c, measureKey, count: true));
        var sum = fan.Merge(cells, c => EntitySum(c, measureKey, count: false));
        if (aggregation == Aggregation.Sum) return sum;
        var n = fan.Merge(cells, c => EntityNumbers(c, measureKey));
        return n == 0 ? null : sum / n;
    }

    private static Distribution Bucketize(List<double> numbers, double? binning) {
        if (numbers.Count == 0) return new Distribution([]);
        var total = (double)numbers.Count;
        return new Distribution([.. numbers
            .GroupBy(v => binning is { } b && b > 0 ? Math.Floor(v / b) * b : v)
            .OrderBy(g => g.Key)
            .Select(g => new DistributionBucket(g.Key, g.Count(), g.Count() / total))]);
    }

    private static double MeasureValue(SnapshotCell cell, string measureKey, Func<MeasureCellStat, double> selector) =>
        cell.Measures.TryGetValue(measureKey, out var stat) ? selector(stat) : 0;

    private static double? MergeExtreme(List<SnapshotCell> cells, string measureKey, bool isMin) {
        double? result = null;
        foreach (var cell in cells) {
            if (!cell.Measures.TryGetValue(measureKey, out var stat)) continue;
            var candidate = isMin ? stat.Min : stat.Max;
            if (candidate is not { } v) continue;
            if (result is not { } r) {
                result = v;
                continue;
            }
            result = isMin ? Math.Min(r, v) : Math.Max(r, v);
        }
        return result;
    }

    private static double DistinctCount(List<SnapshotCell> cells, IReadOnlyList<ReportDimension> grainDims, string measureKey) {
        var axis = -1;
        for (var i = 0; i < grainDims.Count; i++) {
            if (grainDims[i].Field.Answers(measureKey)) {
                axis = i;
                break;
            }
        }
        if (axis < 0) return 0;
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cell in cells) {
            if (cell.Rows > 0) set.Add(cell.Keys[axis]);
        }
        return set.Count;
    }

    private static Distribution MergeDistribution(List<SnapshotCell> cells, string measureKey) {
        var counts = new Dictionary<double, int>();
        foreach (var cell in cells) {
            if (!cell.Measures.TryGetValue(measureKey, out var stat) || stat.Distribution is null) continue;
            foreach (var pair in stat.Distribution) counts[pair.Key] = counts.GetValueOrDefault(pair.Key) + pair.Value;
        }
        if (counts.Count == 0) return new Distribution([]);
        var total = (double)counts.Values.Sum();
        return new Distribution([.. counts.OrderBy(kv => kv.Key).Select(kv => new DistributionBucket(kv.Key, kv.Value, kv.Value / total))]);
    }

    private static MeasureRequest? DenominatorRequest<TRow>(ReportSource<TRow> source, Normalize normalize) {
        if (normalize.Kind != NormalizeKind.By || normalize.Measure is not { } key) return null;
        if (string.Equals(key, MeasureRequest.RowCountKey, StringComparison.Ordinal)) return MeasureRequest.RowCount;
        return source.Measure(key) is { } measure ? new MeasureRequest(key, measure.Default) : null;
    }

    private static (double? Value, Distribution? Dist) Aggregate<TRow>(
        List<SnapshotCell> cells, MeasureRequest request, ReportSource<TRow> source, IReadOnlyList<ReportDimension> grainDims, FanPlan fan) {
        if (request.IsRowCount) return (cells.Sum(c => (double)c.Rows), null);
        var measure = source.Measure(request.Key);
        if (measure is null) return (null, null);
        if (measure.DistinctBy is not null) return AggregateEntities(cells, measure, request.Aggregation, fan);
        var dist = measure.Kind == MeasureKind.Distribution ? MergeDistribution(cells, measure.Key) : null;
        return request.Aggregation switch {
            Aggregation.Count => (MergeMeasureSum(cells, measure.Key, s => s.Rows), dist),
            Aggregation.Sum => (MergeMeasureSum(cells, measure.Key, s => s.Sum), dist),
            Aggregation.Avg => (Average(cells, measure.Key), dist),
            Aggregation.Min => (MergeExtreme(cells, measure.Key, isMin: true), dist),
            Aggregation.Max => (MergeExtreme(cells, measure.Key, isMin: false), dist),
            Aggregation.Distinct => (DistinctCount(cells, grainDims, measure.Key), dist),
            _ => (null, dist),
        };
    }

    private static double? Average(List<SnapshotCell> cells, string measureKey) {
        var sum = MergeMeasureSum(cells, measureKey, s => s.Sum);
        var count = MergeMeasureSum(cells, measureKey, s => s.NumberCount);
        return count == 0 ? null : sum / count;
    }

    private static string Id(IEnumerable<string> keys) => string.Join(Separator, keys);
}
