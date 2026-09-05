using EggIdentity.Fallback;
using EggIdentity.Styles;
using EggIdentity.Styles.Theming;

namespace EggIdentity.Host.CssBuild;

public static class HostPalette {
    private const string ColorPrefix = "--color-";

    public static IReadOnlyList<(string Name, string Value)> ComponentColors { get; } = [
        .. ComponentTokens.Required.Concat(ComponentTokens.Optional)
            .Select(cssVar => (Name: cssVar[ColorPrefix.Length..], Value: FallbackDefaults.Tokens[cssVar]))
    ];

    public static IReadOnlyList<(string Name, string Value)> AppColors { get; } = [
        ("warn", "#f0b232"),
    ];

    public static IReadOnlyList<string> StatusTokens { get; } = ["accent", "ok", "err"];

    public static IReadOnlyList<string> ContrastBaseTokens { get; } = ["bg", "panel0", "panel", "panel2", "fg", "muted", "border"];

    public static ThemeTokenRegistry BuildRegistry() {
        var registry = new ThemeTokenRegistry();
        foreach (var (name, _) in AppColors) {
            registry.Register(name);
        }
        return registry;
    }
}
