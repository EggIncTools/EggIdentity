namespace EggIdentity.Settings;

public sealed record DriftReport(
    IReadOnlyList<string> SetButUnread,
    IReadOnlyList<string> ReadButUnset,
    IReadOnlyList<string> Matched) {

    public bool IsClean => SetButUnread.Count == 0 && ReadButUnset.Count == 0;

    public static DriftReport Compare(SettingsRegistry registry, IEnumerable<string> stackKeys) {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(stackKeys);

        var stack = new HashSet<string>(stackKeys, StringComparer.Ordinal);
        var known = new HashSet<string>(registry.All.Select(d => d.EnvKey), StringComparer.Ordinal);

        var setButUnread = stack.Except(known, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var matched = stack.Intersect(known, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        var readButUnset = registry.All
            .Where(d => d.Required && !stack.Contains(d.EnvKey) && string.IsNullOrEmpty(d.Default))
            .Select(d => d.EnvKey)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new DriftReport(setButUnread, readButUnset, matched);
    }
}
