namespace EggIdentity.Reports;

public sealed record ReportPreset(string Id, string Title, ReportDefinition Definition) {
    public string? Description { get; init; }
}

public static class ReportPresets {
    public static IReadOnlyList<ReportPreset> Runnable(IEnumerable<ReportPreset> presets, ReportTier tier, ReportEngineRegistry registry) {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(registry);
        return [.. presets.Where(p => registry.Validate(p.Definition.Query, tier).Allowed)];
    }

    public static void Check(IReportSource source) {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var preset in source.Presets) {
            var query = preset.Definition.Query;
            if (!string.Equals(query.Source, source.Id, StringComparison.Ordinal))
                throw new ArgumentException($"preset \"{preset.Id}\" targets source \"{query.Source}\", not \"{source.Id}\"", nameof(source));
            foreach (var key in query.GroupBy.Select(g => g.Dimension).Where(k => source.Dimension(k) is null))
                throw new ArgumentException($"preset \"{preset.Id}\" groups by unknown dimension \"{key}\"", nameof(source));
            foreach (var key in query.Measures.Where(m => !m.IsRowCount).Select(m => m.Key).Where(k => source.Measure(k) is null))
                throw new ArgumentException($"preset \"{preset.Id}\" uses unknown measure \"{key}\"", nameof(source));
        }
    }
}
