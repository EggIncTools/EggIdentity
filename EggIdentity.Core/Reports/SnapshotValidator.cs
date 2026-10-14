using EggIdentity.Filters;

namespace EggIdentity.Reports;

internal static class SnapshotValidator {
    public static ReportValidation Validate<TRow>(
        ReportQuery query, ReportSource<TRow> source, SnapshotSchema schema, ReportTierCaps caps,
        IReadOnlyDictionary<string, SnapshotCell>? cells, IReadOnlyList<ReportDimension> grainDims) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(caps);
        var grainByKey = new Dictionary<string, TimeBucket?>(StringComparer.Ordinal);
        for (var i = 0; i < grainDims.Count; i++) grainByKey[grainDims[i].Field.Key] = schema.Grain[i].Bucket;

        var rejections = new List<ReportRejection>();

        if (query.GroupBy.Count > caps.MaxDimensions)
            rejections.Add(new ReportRejection(ReportRejectionKind.DimensionCap, "", $"{query.GroupBy.Count} dimensions exceeds cap of {caps.MaxDimensions}"));

        ValidateGroupBy(query, source, grainByKey, rejections);
        ValidateMeasures(query, source, schema, grainByKey, rejections);
        ValidateFilters(query, source, grainByKey, rejections);

        if (rejections.Count == 0 && cells is not null)
            ValidateCaps(query, source, schema, grainDims, cells, caps, rejections);

        return rejections.Count == 0 ? ReportValidation.Ok : new ReportValidation(rejections);
    }

    private static void ValidateGroupBy<TRow>(
        ReportQuery query, ReportSource<TRow> source, Dictionary<string, TimeBucket?> grainByKey, List<ReportRejection> rejections) {
        foreach (var key in query.GroupBy) {
            var dim = source.Dimension(key.Dimension);
            if (dim is null || !grainByKey.TryGetValue(dim.Field.Key, out var grainBucket)) {
                rejections.Add(new ReportRejection(ReportRejectionKind.DimensionNotInGrain, key.Dimension, $"\"{key.Dimension}\" is not part of the snapshot grain"));
                continue;
            }
            if (!BucketDerivable(grainBucket ?? TimeBucket.Day, key.Bucket)) {
                rejections.Add(new ReportRejection(
                    ReportRejectionKind.BucketNotDerivable, key.Dimension, $"bucket {key.Bucket} cannot derive from grain bucket {grainBucket}"));
            }
        }
    }

    private static bool BucketDerivable(TimeBucket grainBucket, TimeBucket? queryBucket) {
        if (queryBucket is not { } q) return true;
        if (q == grainBucket) return true;
        return grainBucket switch {
            TimeBucket.Day => q is TimeBucket.Week or TimeBucket.Month or TimeBucket.Year,
            TimeBucket.Month => q == TimeBucket.Year,
            _ => false,
        };
    }

    private static void ValidateMeasures<TRow>(
        ReportQuery query, ReportSource<TRow> source, SnapshotSchema schema, Dictionary<string, TimeBucket?> grainByKey, List<ReportRejection> rejections) {
        foreach (var m in query.EffectiveMeasures) {
            if (m.IsRowCount) continue;
            if (!MeasureMergeable(m.Key, m.Aggregation, source, schema, grainByKey, out var reason))
                rejections.Add(new ReportRejection(ReportRejectionKind.MeasureNotMergeable, m.Key, reason));
        }
        if (query.Normalize is { Kind: NormalizeKind.By, Measure: { } denomKey } && !string.Equals(denomKey, MeasureRequest.RowCountKey, StringComparison.Ordinal)) {
            var aggregation = source.Measure(denomKey)?.Default ?? Aggregation.Sum;
            if (!MeasureMergeable(denomKey, aggregation, source, schema, grainByKey, out var reason))
                rejections.Add(new ReportRejection(ReportRejectionKind.MeasureNotMergeable, denomKey, reason));
        }
    }

    private static bool MeasureMergeable<TRow>(
        string key, Aggregation aggregation, ReportSource<TRow> source, SnapshotSchema schema, Dictionary<string, TimeBucket?> grainByKey, out string reason) {
        if (!schema.Measures.Contains(key, StringComparer.Ordinal)) {
            reason = $"\"{key}\" is not declared in the snapshot schema";
            return false;
        }
        var measure = source.Measure(key);
        if (measure is null) {
            reason = $"\"{key}\" is not a measure on source \"{source.Id}\"";
            return false;
        }
        if (aggregation == Aggregation.Distinct && !grainByKey.ContainsKey(key)) {
            reason = $"distinct(\"{key}\") requires the key to be a grain dimension";
            return false;
        }
        reason = "";
        return true;
    }

    private static void ValidateFilters<TRow>(
        ReportQuery query, ReportSource<TRow> source, Dictionary<string, TimeBucket?> grainByKey, List<ReportRejection> rejections) {
        foreach (var condition in AllFilterConditions(query.Filters)) {
            var field = FilterFields.Find(source.Catalog.Fields, condition.Field);
            if (field is null || !grainByKey.TryGetValue(field.Key, out var grainBucket)) {
                rejections.Add(new ReportRejection(ReportRejectionKind.FilterNotInGrain, condition.Field, $"\"{condition.Field}\" is not part of the snapshot grain"));
                continue;
            }
            if (field.Kind == FilterValueKind.Date && (grainBucket ?? TimeBucket.Day) != TimeBucket.Day) {
                rejections.Add(new ReportRejection(
                    ReportRejectionKind.FilterNotInGrain, condition.Field, $"date filters need a Day-bucketed grain, \"{condition.Field}\" is bucketed {grainBucket}"));
            }
        }
    }

    private static IEnumerable<FilterCondition> AllFilterConditions(FilterSet filters) {
        foreach (var c in filters.And) yield return c;
        foreach (var group in filters.Or) {
            foreach (var c in group) yield return c;
        }
    }

    private static void ValidateCaps<TRow>(
        ReportQuery query, ReportSource<TRow> source, SnapshotSchema schema, IReadOnlyList<ReportDimension> grainDims,
        IReadOnlyDictionary<string, SnapshotCell> cells, ReportTierCaps caps, List<ReportRejection> rejections) {
        var rollup = SnapshotRollup.Build(cells.Values, query, source, schema, grainDims, query.EffectiveMeasures);
        if (rollup.Rows.Count > caps.MaxCells)
            rejections.Add(new ReportRejection(ReportRejectionKind.CellCap, "", $"query would produce {rollup.Rows.Count} cells, cap is {caps.MaxCells}"));
        for (var axis = 0; axis < query.GroupBy.Count; axis++) {
            if (query.GroupBy[axis].Bucket is null) continue;
            var distinct = rollup.Rows.Select(r => r.Keys[axis]).Distinct(StringComparer.Ordinal).Count();
            if (distinct > caps.MaxBuckets) {
                rejections.Add(new ReportRejection(
                    ReportRejectionKind.BucketCap, query.GroupBy[axis].Dimension, $"bucket axis \"{query.GroupBy[axis].Dimension}\" would produce {distinct} buckets, cap is {caps.MaxBuckets}"));
            }
        }
    }
}
