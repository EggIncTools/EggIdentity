namespace EggIdentity.Styles.Theming;

public sealed record ThemeToken(string CssName);

public sealed class ThemeTokenRegistry {
    private readonly List<ThemeToken> _appTokens = [];

    public static IReadOnlyList<string> BaselineSettable { get; } =
        ComponentTokens.Required.Concat(ComponentTokens.Optional)
            .Select(StripColorPrefix)
            .ToArray();

    public IReadOnlyList<ThemeToken> AppTokens => _appTokens;

    public ThemeTokenRegistry Register(string cssName) {
        _appTokens.Add(new ThemeToken(cssName));
        return this;
    }

    public bool IsKnown(string cssName) => Canonicalize(cssName) is not null;

    public string? Canonicalize(string cssName) {
        foreach (string name in BaselineSettable) {
            if (string.Equals(name, cssName, StringComparison.Ordinal)) return name;
        }

        foreach (var token in _appTokens) {
            if (string.Equals(token.CssName, cssName, StringComparison.Ordinal)) return token.CssName;
        }

        return null;
    }

    private static string StripColorPrefix(string cssVar) =>
        cssVar.StartsWith("--color-", StringComparison.Ordinal) ? cssVar["--color-".Length..] : cssVar;
}
