namespace EggIdentity.Reports;

public sealed record DistributionBucket(double Value, int Count, double Share);

public sealed record Distribution(IReadOnlyList<DistributionBucket> Buckets);

public sealed record CubeRow(IReadOnlyList<string> Keys, IReadOnlyList<double?> Values, int Samples) {
    public IReadOnlyList<Distribution?> Distributions { get; init; } = [];
}

public sealed record ReportCube(IReadOnlyList<string> Dimensions, IReadOnlyList<MeasureRequest> Measures, bool Normalized, IReadOnlyList<CubeRow> Rows) {
    public int MeasureIndex(string key) {
        ArgumentNullException.ThrowIfNull(key);
        for (var i = 0; i < Measures.Count; i++)
            if (string.Equals(Measures[i].Key, key, StringComparison.Ordinal)) return i;
        return -1;
    }

    public int DimensionIndex(string key) {
        ArgumentNullException.ThrowIfNull(key);
        for (var i = 0; i < Dimensions.Count; i++)
            if (string.Equals(Dimensions[i], key, StringComparison.Ordinal)) return i;
        return -1;
    }
}

public enum ReportCostLevel {
    Low,
    Medium,
    High,
}

public sealed record ReportCost(ReportCostLevel Level, string? Note = null);

public interface IReportCostEstimator {
    ReportCost Estimate(ReportQuery query);
}

public interface IReportEngine {
    Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct);
}

public sealed class ReportEngineRegistry {
    private readonly Dictionary<string, IReportEngine> _engines = [];
    private readonly Dictionary<string, ISnapshotEngine> _snapshots = [];
    private readonly Dictionary<ReportTier, ReportTierCaps> _caps = new() {
        [ReportTier.Live] = ReportTierCaps.Unlimited,
        [ReportTier.Snapshot] = ReportTierCaps.Unlimited,
    };

    public IReadOnlyCollection<string> Sources => _engines.Keys;

    public ReportEngineRegistry Add(string source, IReportEngine engine) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(engine);
        _engines[source] = engine;
        return this;
    }

    public ReportEngineRegistry AddSnapshot(string source, ISnapshotEngine snapshot) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshots[source] = snapshot;
        return this;
    }

    public IReportEngine? Find(string source) {
        ArgumentNullException.ThrowIfNull(source);
        return _engines.GetValueOrDefault(source);
    }

    public ISnapshotEngine? Snapshot(string source) {
        ArgumentNullException.ThrowIfNull(source);
        return _snapshots.GetValueOrDefault(source);
    }

    public ReportEngineRegistry SetCaps(ReportTier tier, ReportTierCaps caps) {
        ArgumentNullException.ThrowIfNull(caps);
        _caps[tier] = caps;
        return this;
    }

    public ReportTierCaps Caps(ReportTier tier) => _caps.GetValueOrDefault(tier, ReportTierCaps.Unlimited);

    public ReportValidation Validate(ReportQuery query, ReportTier tier) {
        ArgumentNullException.ThrowIfNull(query);
        if (tier == ReportTier.Snapshot) return ValidateSnapshot(query);

        List<ReportRejection> rejections = [];
        if (Find(query.Source) is null)
            rejections.Add(new ReportRejection(ReportRejectionKind.UnknownKey, query.Source, $"unknown source \"{query.Source}\""));

        var caps = Caps(tier);
        if (query.GroupBy.Count > caps.MaxDimensions)
            rejections.Add(new ReportRejection(ReportRejectionKind.DimensionCap, query.Source, $"group by exceeds max dimensions ({caps.MaxDimensions})"));

        return rejections.Count == 0 ? ReportValidation.Ok : new ReportValidation(rejections);
    }

    public async Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);

        var validation = Validate(query, context.Tier);
        if (!validation.Allowed) {
            if (validation.Rejections.Any(r => r.Kind == ReportRejectionKind.UnknownKey))
                throw new KeyNotFoundException($"no report engine for source \"{query.Source}\"");
            throw new ReportRejectedException(validation.Rejections);
        }

        var cube = query.Normalize.ForeignTo(query.Source)
            ? await RunForeignNormalizedAsync(this, query, context, ct).ConfigureAwait(false)
            : context.Tier == ReportTier.Snapshot
                ? await Snapshot(query.Source)!.RunAsync(query, context, ct).ConfigureAwait(false)
                : await Find(query.Source)!.RunAsync(query, context, ct).ConfigureAwait(false);

        var caps = Caps(context.Tier);
        List<ReportRejection> postRejections = [];
        if (cube.Rows.Count > caps.MaxCells)
            postRejections.Add(new ReportRejection(ReportRejectionKind.CellCap, query.Source, $"result has {cube.Rows.Count} cells, exceeds cap of {caps.MaxCells}"));

        var maxBuckets = MaxBucketCount(cube);
        if (maxBuckets > caps.MaxBuckets)
            postRejections.Add(new ReportRejection(ReportRejectionKind.BucketCap, query.Source, $"result has {maxBuckets} buckets, exceeds cap of {caps.MaxBuckets}"));

        if (postRejections.Count > 0) throw new ReportRejectedException(postRejections);
        return cube;
    }

    public void Invalidate(string source) {
        ArgumentNullException.ThrowIfNull(source);
        Snapshot(source)?.Invalidate();
    }

    public bool Remove(string source) {
        ArgumentNullException.ThrowIfNull(source);
        if (_snapshots.Remove(source, out var snapshot) && snapshot is IDisposable disposable) disposable.Dispose();
        return _engines.Remove(source);
    }

    public ReportEngineRegistry AddSnapshotSource(SourceDescriptor descriptor, RowSnapshot snapshot, SnapshotSchema? schema, TimeProvider clock) {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(clock);
        if (!string.Equals(snapshot.Source, descriptor.Id, StringComparison.Ordinal))
            throw new ArgumentException($"snapshot source \"{snapshot.Source}\" does not match descriptor \"{descriptor.Id}\"", nameof(snapshot));

        var source = SnapshotSource.Create(descriptor, snapshot);
        IReadOnlyList<int>? cachedRows = null;
        ValueTask<IReadOnlyList<int>> Rows(CancellationToken _) {
            cachedRows ??= [.. Enumerable.Range(0, snapshot.Rows)];
            return ValueTask.FromResult(cachedRows);
        }
        Add(descriptor.Id, new InMemoryReportEngine<int>(source, Rows, clock));

        if (schema is not null) {
            AddSnapshot(descriptor.Id, new SnapshotReportEngine<int>(source, schema, Rows, _ => ValueTask.FromResult(snapshot.Version), clock));
        }

        return this;
    }

    private ReportValidation ValidateSnapshot(ReportQuery query) {
        if (Snapshot(query.Source) is { } snapshot) return snapshot.Validate(query, Caps(ReportTier.Snapshot));
        return new ReportValidation([new ReportRejection(ReportRejectionKind.NoSnapshot, query.Source, $"no snapshot engine for source \"{query.Source}\"")]);
    }

    private static async Task<ReportCube> RunForeignNormalizedAsync(ReportEngineRegistry registry, ReportQuery query, ReportContext context, CancellationToken ct) {
        var mainQuery = query with { Normalize = Normalize.None };
        var mainTask = context.Tier == ReportTier.Snapshot
            ? registry.Snapshot(query.Source)!.RunAsync(mainQuery, context, ct)
            : registry.Find(query.Source)!.RunAsync(mainQuery, context, ct);

        var foreignEngine = registry.Find(query.Normalize.Source!)
            ?? throw new KeyNotFoundException($"no report engine for source \"{query.Normalize.Source}\"");
        var denominatorQuery = new ReportQuery(query.Normalize.Source!) {
            GroupBy = query.GroupBy,
            Filters = query.Filters,
            Measures = [query.Normalize.Measure == MeasureRequest.RowCountKey ? MeasureRequest.RowCount : new MeasureRequest(query.Normalize.Measure!, Aggregation.Sum)],
        };
        var denominatorTask = foreignEngine.RunAsync(denominatorQuery, context, ct);

        await Task.WhenAll(mainTask, denominatorTask).ConfigureAwait(false);
        var mainCube = await mainTask.ConfigureAwait(false);
        var denominatorCube = await denominatorTask.ConfigureAwait(false);

        var denominators = new Dictionary<string, double?>();
        foreach (var row in denominatorCube.Rows)
            denominators[string.Join('\u001f', row.Keys)] = row.Values[0];

        var rows = new List<CubeRow>(mainCube.Rows.Count);
        foreach (var row in mainCube.Rows) {
            var denominator = denominators.GetValueOrDefault(string.Join('\u001f', row.Keys));
            var values = row.Values.Select(v => v is null || denominator is null || denominator == 0 ? (double?)null : v / denominator).ToList();
            rows.Add(row with { Values = values });
        }

        return mainCube with { Rows = rows, Normalized = true };
    }

    private static int MaxBucketCount(ReportCube cube) {
        var max = 0;
        foreach (var row in cube.Rows) {
            foreach (var distribution in row.Distributions) {
                if (distribution is null) continue;
                if (distribution.Buckets.Count > max) max = distribution.Buckets.Count;
            }
        }
        return max;
    }
}
