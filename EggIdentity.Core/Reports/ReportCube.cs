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

    public IReadOnlyCollection<string> Sources => _engines.Keys;

    public ReportEngineRegistry Add(string source, IReportEngine engine) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(engine);
        _engines[source] = engine;
        return this;
    }

    public IReportEngine? Find(string source) {
        ArgumentNullException.ThrowIfNull(source);
        return _engines.GetValueOrDefault(source);
    }

    public Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(query);
        return Find(query.Source) is { } engine
            ? engine.RunAsync(query, context, ct)
            : throw new KeyNotFoundException($"no report engine for source \"{query.Source}\"");
    }
}
