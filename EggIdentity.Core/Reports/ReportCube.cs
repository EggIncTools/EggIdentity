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

        var cube = context.Tier == ReportTier.Snapshot
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

    private ReportValidation ValidateSnapshot(ReportQuery query) {
        if (Snapshot(query.Source) is { } snapshot) return snapshot.Validate(query, Caps(ReportTier.Snapshot));
        return new ReportValidation([new ReportRejection(ReportRejectionKind.NoSnapshot, query.Source, $"no snapshot engine for source \"{query.Source}\"")]);
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
