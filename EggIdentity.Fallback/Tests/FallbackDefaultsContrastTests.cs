using EggIdentity.Styles;
using EggIdentity.Styles.Theming;

namespace EggIdentity.Fallback.Tests;

public class FallbackDefaultsContrastTests {
    private static readonly string[] StatusTokens = ["accent", "ok", "err"];

    private static IReadOnlyDictionary<string, ThemeColor> Palette() =>
        FallbackDefaults.Tokens.ToDictionary(
            pair => pair.Key["--color-".Length..],
            pair => ThemeColor.FromHex(pair.Value)!.Value,
            StringComparer.Ordinal);

    [Fact]
    public void Defaults_PassContrastGate() {
        var result = ThemeContrast.Validate(Palette(), ThemeChroma.None, StatusTokens);

        Assert.True(result.Passes, string.Join("; ",
            result.Failures.Select(f => $"{f.Check} {f.A}/{f.B} {f.Measured} < {f.Required}")));
    }

    [Fact]
    public void Defaults_CoverEveryRequiredToken() {
        foreach (var token in ComponentTokens.Required) {
            Assert.True(FallbackDefaults.Tokens.ContainsKey(token), token);
        }
    }
}
