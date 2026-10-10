namespace EggIdentity.Styles;

internal sealed class StableKeyComparer : IEqualityComparer<string> {
    public static readonly StableKeyComparer Instance = new();

    public bool Equals(string? x, string? y) => string.Equals(x, y, StringComparison.Ordinal);

    public int GetHashCode(string obj) {
        var hash = 2166136261u;
        foreach (var c in obj) hash = unchecked((hash ^ c) * 16777619u);
        return unchecked((int)hash);
    }
}
