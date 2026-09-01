namespace EggIdentity.Styles.Theming;

public enum ThemePropertyGroup {
    ColorOnly,
    Surface,
    Text,
    Full
}

public sealed record ThemeCatalogEntry(string Name, string Selector, ThemePropertyGroup Group);

public sealed class ThemeCssCatalog(IReadOnlyList<ThemeCatalogEntry> entries) {
    public IReadOnlyList<ThemeCatalogEntry> Entries { get; } = entries;

    private readonly Dictionary<string, ThemeCatalogEntry> _byName =
        entries.ToDictionary(e => e.Name, StringComparer.Ordinal);

    private static readonly Dictionary<string, ThemePropertyGroup> PropertyFloor = new(StringComparer.Ordinal) {
        ["color"] = ThemePropertyGroup.ColorOnly,
        ["background-color"] = ThemePropertyGroup.ColorOnly,
        ["border-color"] = ThemePropertyGroup.ColorOnly,
        ["border-width"] = ThemePropertyGroup.Surface,
        ["border-style"] = ThemePropertyGroup.Surface,
        ["border-radius"] = ThemePropertyGroup.Surface,
        ["box-shadow"] = ThemePropertyGroup.Surface,
        ["background-image"] = ThemePropertyGroup.Surface,
        ["font-weight"] = ThemePropertyGroup.Text,
        ["font-style"] = ThemePropertyGroup.Text,
        ["letter-spacing"] = ThemePropertyGroup.Text,
        ["text-transform"] = ThemePropertyGroup.Text,
        ["text-decoration-line"] = ThemePropertyGroup.Text,
        ["text-decoration-color"] = ThemePropertyGroup.Text,
        ["transition-duration"] = ThemePropertyGroup.Full,
        ["transition-property"] = ThemePropertyGroup.Full,
        ["outline-color"] = ThemePropertyGroup.Full,
        ["caret-color"] = ThemePropertyGroup.Full,
        ["accent-color"] = ThemePropertyGroup.Full,
        ["opacity"] = ThemePropertyGroup.Full
    };

    public ThemeCatalogEntry? Find(string name) =>
        _byName.TryGetValue(name, out var entry) ? entry : null;

    public static string? CanonicalProperty(string name) =>
        PropertyFloor.TryGetValue(name, out _)
            ? PropertyFloor.Keys.First(k => string.Equals(k, name, StringComparison.Ordinal))
            : null;

    public static bool Allows(ThemePropertyGroup group, string canonicalProperty) =>
        PropertyFloor.TryGetValue(canonicalProperty, out var floor) && floor <= group;
}
