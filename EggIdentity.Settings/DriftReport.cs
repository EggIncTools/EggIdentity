namespace EggIdentity.Settings;

public enum DriftReason {
    Undeclared,
    UnusedStackVariable,
    MissingRequired,
    MissingOptional,
    External,
    Matched
}

public sealed record DriftEntry(string Key, EnvOrigin? Origin, DriftReason Reason, string? Detail);

public sealed record DriftReport(IReadOnlyList<DriftEntry> Entries) {
    public IReadOnlyList<DriftEntry> Undeclared => Of(DriftReason.Undeclared);
    public IReadOnlyList<DriftEntry> UnusedStackVariables => Of(DriftReason.UnusedStackVariable);
    public IReadOnlyList<DriftEntry> MissingRequired => Of(DriftReason.MissingRequired);
    public IReadOnlyList<DriftEntry> MissingOptional => Of(DriftReason.MissingOptional);
    public IReadOnlyList<DriftEntry> External => Of(DriftReason.External);
    public IReadOnlyList<DriftEntry> Matched => Of(DriftReason.Matched);

    public int ProblemCount => Entries.Count(e => IsProblem(e.Reason));

    public bool IsClean => ProblemCount == 0;

    public static bool IsProblem(DriftReason reason) =>
        reason is DriftReason.Undeclared or DriftReason.UnusedStackVariable or DriftReason.MissingRequired;

    private IReadOnlyList<DriftEntry> Of(DriftReason reason) => [.. Entries.Where(e => e.Reason == reason)];

    public static DriftReport Compare(
        SettingsRegistry registry, IEnumerable<EnvKeyInfo> env, IReadOnlySet<string>? databaseKeys = null) {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(env);

        var present = Dedupe(env);
        var declared = registry.All
            .GroupBy(d => d.EnvKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var entries = present.Values
            .OrderBy(i => i.Name, StringComparer.Ordinal)
            .Select(i => ClassifyPresent(i, declared))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        entries.AddRange(declared.Values
            .OrderBy(d => d.EnvKey, StringComparer.Ordinal)
            .Select(d => ClassifyMissing(d, present, databaseKeys))
            .Where(e => e is not null)
            .Select(e => e!));

        return new DriftReport(entries);
    }

    private static Dictionary<string, EnvKeyInfo> Dedupe(IEnumerable<EnvKeyInfo> env) {
        var byName = new Dictionary<string, EnvKeyInfo>(StringComparer.Ordinal);
        foreach (var info in env) {
            if (!byName.TryGetValue(info.Name, out var existing) || Rank(info.Origin) < Rank(existing.Origin))
                byName[info.Name] = info;
        }
        return byName;
    }

    private static DriftEntry? ClassifyPresent(EnvKeyInfo info, Dictionary<string, SettingDescriptor> declared) {
        if (info.Origin == EnvOrigin.StackVariable) {
            return info.Referenced
                ? null
                : new DriftEntry(info.Name, info.Origin, DriftReason.UnusedStackVariable, "no service interpolates this stack variable");
        }
        if (declared.TryGetValue(info.Name, out var d)) {
            var reason = d.Kind == SettingKind.External ? DriftReason.External : DriftReason.Matched;
            return new DriftEntry(info.Name, info.Origin, reason, d.Description);
        }
        return info.Origin is EnvOrigin.ServiceEnvironment or EnvOrigin.EnvFile
            ? new DriftEntry(info.Name, info.Origin, DriftReason.Undeclared, "no descriptor declares this key")
            : null;
    }

    private static DriftEntry? ClassifyMissing(
        SettingDescriptor d, Dictionary<string, EnvKeyInfo> present, IReadOnlySet<string>? databaseKeys) {
        if (present.TryGetValue(d.EnvKey, out var info) && info.IsPresentInContainer) return null;
        if (!string.IsNullOrEmpty(d.Default)) return null;
        if (d.Tier != ApplyTier.Bootstrap && databaseKeys?.Contains(d.Key) == true) return null;
        var reason = d.Required ? DriftReason.MissingRequired : DriftReason.MissingOptional;
        return new DriftEntry(d.EnvKey, null, reason, d.Label);
    }

    private static int Rank(EnvOrigin origin) => origin switch {
        EnvOrigin.Image => 0,
        EnvOrigin.ServiceEnvironment => 1,
        EnvOrigin.EnvFile => 2,
        EnvOrigin.Runtime => 3,
        _ => 4,
    };
}
