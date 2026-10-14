namespace EggIdentity.Reports;

public sealed record SnapshotSchema(string Source, IReadOnlyList<GroupKey> Grain, IReadOnlyList<string> Measures);

public interface ISnapshotEngine : IReportEngine {
    SnapshotSchema Schema { get; }

    ReportValidation Validate(ReportQuery query, ReportTierCaps caps);

    void Invalidate();

    string? Version { get; }
}
