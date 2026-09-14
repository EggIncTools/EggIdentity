namespace EggIdentity.Settings;

public sealed record NavCategory(string Category, string Label, int Count);

public sealed record NavGroup(string Key, string Label, IReadOnlyList<NavCategory> Categories) {
    public int Count => Categories.Sum(c => c.Count);

    public bool IsLeaf => Categories.Count == 1;

    public bool Covers(string category) =>
        Categories.Any(c => string.Equals(c.Category, category, StringComparison.Ordinal));
}

public static class SettingsNav {
    public const string Separator = ": ";
    public const string OverflowKey = "More";
    private const int FoldBelow = 3;
    private const int FoldWhenAtLeast = 2;

    public static IReadOnlyList<NavGroup> Build(IEnumerable<SettingDescriptor> descriptors) {
        ArgumentNullException.ThrowIfNull(descriptors);

        var counts = descriptors
            .GroupBy(d => d.Category, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var groups = counts
            .GroupBy(pair => GroupOf(pair.Key), StringComparer.Ordinal)
            .Select(g => new NavGroup(g.Key, g.Key, [.. g
                .Select(pair => new NavCategory(pair.Key, LabelOf(pair.Key), pair.Value))
                .OrderBy(c => c.Label, StringComparer.Ordinal)]))
            .OrderBy(g => g.Label, StringComparer.Ordinal)
            .ToList();

        return Fold(groups);
    }

    public static string GroupOf(string category) {
        ArgumentNullException.ThrowIfNull(category);
        var index = category.IndexOf(Separator, StringComparison.Ordinal);
        return index <= 0 ? category : category[..index];
    }

    public static string LabelOf(string category) {
        ArgumentNullException.ThrowIfNull(category);
        var index = category.IndexOf(Separator, StringComparison.Ordinal);
        return index <= 0 ? category : category[(index + Separator.Length)..];
    }

    private static List<NavGroup> Fold(List<NavGroup> groups) {
        var small = groups.Where(g => g.IsLeaf && g.Count < FoldBelow).ToList();
        if (small.Count < FoldWhenAtLeast) return groups;

        var kept = groups.Where(g => !small.Contains(g)).ToList();
        var folded = new NavGroup(OverflowKey, OverflowKey, [.. small
            .SelectMany(g => g.Categories)
            .OrderBy(c => c.Label, StringComparer.Ordinal)]);

        kept.Add(folded);
        return kept;
    }
}
