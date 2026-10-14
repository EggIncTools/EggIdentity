using System.Globalization;
using EggIdentity.Filters;

namespace EggIdentity.Reports;

public sealed class SnapshotReportEngine<TRow>(
    ReportSource<TRow> source, SnapshotSchema schema,
    Func<CancellationToken, ValueTask<IReadOnlyList<TRow>>> rows,
    Func<CancellationToken, ValueTask<string>> version, TimeProvider clock) : ISnapshotEngine, IDisposable {
    private const char Separator = '\u001f';

    private readonly List<ReportDimension> _grainDims = [.. schema.Grain.Select(g =>
        source.Dimension(g.Dimension) ?? throw new ArgumentException($"snapshot grain references unknown dimension \"{g.Dimension}\"", nameof(schema)))];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlyDictionary<string, SnapshotCell>? _cells;
    private volatile string? _builtVersion;
    private volatile bool _invalidated = true;

    public SnapshotSchema Schema => schema;

    public string? Version => _builtVersion;

    public void Invalidate() => _invalidated = true;

    public void Dispose() => _gate.Dispose();

    public ReportValidation Validate(ReportQuery query, ReportTierCaps caps) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(caps);
        return SnapshotValidator.Validate(query, source, schema, caps, _cells, _grainDims);
    }

    public async Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(query.Source, source.Id, StringComparison.Ordinal))
            throw new ArgumentException($"query source \"{query.Source}\" does not match \"{source.Id}\"", nameof(query));

        var validation = Validate(query, ReportTierCaps.Unlimited);
        if (!validation.Allowed) throw new ReportRejectedException(validation.Rejections);

        var cells = await EnsureCellsAsync(ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var queryDims = query.GroupBy.Select(Dimension).ToList();
        var measures = query.EffectiveMeasures;
        var fields = source.Catalog.Fields;
        var evaluation = new FilterEvaluation(source.Scope, Now: now);

        var filtered = cells.Values
            .Where(c => FilterEvaluator.Evaluate(query.Filters, fields, key => CellSubject(c, key), evaluation).Matched)
            .ToList();

        var rollup = SnapshotRollup.Build(filtered, query, source, schema, _grainDims, measures);
        var cubeRows = Fill(rollup.Rows, query, queryDims, measures, context.Zone, now);
        cubeRows = [.. cubeRows.Where(r => CubeShaping.Passes(r, query.Having, measures))];
        var allowed = NormalizeAllowed(query, queryDims);
        var normalized = CubeShaping.Normalize(cubeRows, query.Normalize, allowed, keys => rollup.DenominatorByTarget.GetValueOrDefault(Id(keys)));
        var sorted = CubeShaping.Sort(normalized, query.Sort, queryDims, measures);
        var limited = CubeShaping.Limit(sorted, query.Limit, query.Sort, measures);
        var applied = query.Normalize.Kind != NormalizeKind.None && allowed;
        return new ReportCube([.. queryDims.Select(d => d.Key)], measures, applied, limited);
    }

    private async Task<IReadOnlyDictionary<string, SnapshotCell>> EnsureCellsAsync(CancellationToken ct) {
        var currentVersion = await version(ct).ConfigureAwait(false);
        if (Fresh(currentVersion) && _cells is { } cached) return cached;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try {
            if (Fresh(currentVersion) && _cells is { } settled) return settled;
            try {
                var data = await rows(ct).ConfigureAwait(false);
                var built = SnapshotCellBuilder.Build(data, source, schema, _grainDims, TimeZoneInfo.Utc);
                _cells = built;
                _builtVersion = currentVersion;
                _invalidated = false;
                return built;
            } catch {
                if (_cells is { } previous) return previous;
                throw;
            }
        } finally {
            _gate.Release();
        }
    }

    private bool Fresh(string currentVersion) =>
        !_invalidated && _cells is not null && string.Equals(_builtVersion, currentVersion, StringComparison.Ordinal);

    private FilterSubjectValue CellSubject(SnapshotCell cell, string key) {
        for (var i = 0; i < _grainDims.Count; i++) {
            if (!_grainDims[i].Field.Answers(key)) continue;
            var raw = cell.Keys[i];
            if (_grainDims[i].Field.Kind == FilterValueKind.Date) {
                var bucket = schema.Grain[i].Bucket ?? TimeBucket.Day;
                return TimeBuckets.Parse(raw, bucket) is { } day
                    ? FilterSubjectValue.Of(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
                    : FilterSubjectValue.Absent;
            }
            if (_grainDims[i].Field.Kind == FilterValueKind.Number)
                return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? FilterSubjectValue.Of(n) : FilterSubjectValue.Absent;
            return FilterSubjectValue.Of(raw);
        }
        return FilterSubjectValue.Absent;
    }

    private ReportDimension Dimension(GroupKey key) =>
        source.Dimension(key.Dimension) ?? throw new ArgumentException($"unknown dimension \"{key.Dimension}\"", nameof(key));

    private ReportMeasure Measure(string key) =>
        source.Measure(key) ?? throw new ArgumentException($"unknown measure \"{key}\"", nameof(key));

    private bool NormalizeAllowed(ReportQuery query, List<ReportDimension> dims) {
        if (query.Normalize.Kind != NormalizeKind.By || query.Normalize.Measure is not { } key) return true;
        if (string.Equals(key, MeasureRequest.RowCountKey, StringComparison.Ordinal)) return true;
        var measure = Measure(key);
        return dims.TrueForAll(d => measure.Allows(d.Field.Scope));
    }

    private static string Id(IEnumerable<string> keys) => string.Join(Separator, keys);

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

    private static IReadOnlyList<string> Replace(IReadOnlyList<string> template, int position, string key) =>
        [.. template.Select((k, i) => i == position ? key : k)];

    private static CubeRow Empty(IReadOnlyList<string> keys, IReadOnlyList<MeasureRequest> measures) =>
        new(keys, [.. measures.Select(m => MeasureAccumulator.Empty(m.Aggregation))], 0) {
            Distributions = [.. measures.Select(_ => (Distribution?)null)],
        };
}
