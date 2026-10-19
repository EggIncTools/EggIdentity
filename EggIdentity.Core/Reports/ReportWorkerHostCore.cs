using System.Text.Json;

namespace EggIdentity.Reports;

public sealed class ReportWorkerHostCore(TimeProvider clock) {
    private readonly ReportEngineRegistry _registry = new();
    private readonly Dictionary<string, SourceDescriptor> _descriptors = [];
    private readonly Dictionary<string, string> _versions = [];

    public ReportWorkerHostCore() : this(TimeProvider.System) {
    }

    public void Configure(string capsJson) {
        ArgumentException.ThrowIfNullOrWhiteSpace(capsJson);
        using var document = JsonDocument.Parse(capsJson);
        foreach (var property in document.RootElement.EnumerateObject()) {
            var tier = Enum.Parse<ReportTier>(property.Name, true);
            var caps = property.Value.Deserialize(ReportJsonContext.Default.ReportTierCaps)
                ?? throw new JsonException($"report tier caps for \"{property.Name}\" must not be null");
            _registry.SetCaps(tier, caps);
        }
    }

    public void Describe(string descriptorJson) {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptorJson);
        var descriptor = ReportWire.ReadDescriptor(descriptorJson);
        _descriptors[descriptor.Id] = descriptor;
    }

    public void Load(byte[] snapshot, string? schemaJson) {
        ArgumentNullException.ThrowIfNull(snapshot);
        var rowSnapshot = ReportWire.ReadSnapshot(snapshot);
        if (!_descriptors.TryGetValue(rowSnapshot.Source, out var descriptor))
            throw new KeyNotFoundException($"no descriptor for source \"{rowSnapshot.Source}\"");

        var schema = schemaJson is null ? null : JsonSerializer.Deserialize(schemaJson, ReportJsonContext.Default.SnapshotSchema);
        _registry.AddSnapshotSource(descriptor, rowSnapshot, schema, clock);
        _versions[rowSnapshot.Source] = rowSnapshot.Version;
    }

    public string? Version(string source) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return _versions.GetValueOrDefault(source);
    }

    public void Unload(string source) {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        _versions.Remove(source);
        _registry.Remove(source);
    }

    public async Task<string> Run(string queryJson, string zoneId, string tier) {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tier);

        var query = ReportWire.ReadQuery(queryJson);
        if (!_versions.ContainsKey(query.Source))
            throw new KeyNotFoundException($"no report engine for source \"{query.Source}\"");

        var reportTier = Enum.Parse<ReportTier>(tier, true);
        var context = new ReportContext(ResolveZone(zoneId), reportTier);

        try {
            var cube = await _registry.RunAsync(query, context, CancellationToken.None).ConfigureAwait(false);
            return ReportWire.WriteCube(cube);
        } catch (ReportRejectedException e) {
            throw new InvalidOperationException(JsonSerializer.Serialize(e.Rejections, ReportJsonContext.Default.ReportRejectionArray));
        }
    }

    private static TimeZoneInfo ResolveZone(string zoneId) {
        try {
            return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        } catch (TimeZoneNotFoundException) {
            return TimeZoneInfo.Utc;
        } catch (InvalidTimeZoneException) {
            return TimeZoneInfo.Utc;
        }
    }
}
