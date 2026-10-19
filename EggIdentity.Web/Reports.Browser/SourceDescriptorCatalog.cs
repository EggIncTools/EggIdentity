using Microsoft.Extensions.Logging;

namespace EggIdentity.Reports.Browser;

public sealed class SourceDescriptorCatalog {
    public IReadOnlyDictionary<string, SourceDescriptor> Descriptors { get; }

    public IReadOnlyDictionary<string, SnapshotSchema?> Schemas { get; }

    public IReadOnlyList<string> Downgrades { get; }

    public SourceDescriptorCatalog(
        IEnumerable<IReportSource> sources, IEnumerable<ReportSnapshotCapture> captures, ILogger<SourceDescriptorCatalog> logger) {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(captures);
        ArgumentNullException.ThrowIfNull(logger);

        var descriptors = new Dictionary<string, SourceDescriptor>(StringComparer.Ordinal);
        var downgrades = new List<string>();
        foreach (var source in sources) {
            var (descriptor, sourceDowngrades) = SourceDescriptor.Capture(source);
            descriptors[descriptor.Id] = descriptor;
            downgrades.AddRange(sourceDowngrades);
        }

        var schemas = new Dictionary<string, SnapshotSchema?>(StringComparer.Ordinal);
        foreach (var capture in captures) schemas[capture.Source] = capture.Schema;

        Descriptors = descriptors;
        Schemas = schemas;
        Downgrades = downgrades;

        foreach (var downgrade in downgrades) logger.LogWarning("report source downgrade: {Downgrade}", downgrade);
    }
}
