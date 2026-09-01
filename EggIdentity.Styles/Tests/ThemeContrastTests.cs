using EggIdentity.Styles.Theming;

namespace EggIdentity.Styles.Tests;

public class ThemeContrastTests {
    private static readonly string[] StatusTokens = ["accent", "ok", "err", "info"];

    private static IReadOnlyDictionary<string, ThemeColor> DefaultPalette() => new Dictionary<string, ThemeColor> {
        ["bg"] = ThemeColor.FromHex("#1b1b1f")!.Value,
        ["panel0"] = ThemeColor.FromHex("#202027")!.Value,
        ["panel"] = ThemeColor.FromHex("#25252b")!.Value,
        ["panel2"] = ThemeColor.FromHex("#2e2e36")!.Value,
        ["fg"] = ThemeColor.FromHex("#e7e7ea")!.Value,
        ["muted"] = ThemeColor.FromHex("#9a9aa5")!.Value,
        ["accent"] = ThemeColor.FromHex("#ef7559")!.Value,
        ["info"] = ThemeColor.FromHex("#5aa9e6")!.Value,
        ["ok"] = ThemeColor.FromHex("#5ec27e")!.Value,
        ["err"] = ThemeColor.FromHex("#e0685f")!.Value,
        ["border"] = ThemeColor.FromHex("#3a3a44")!.Value,
    };

    [Fact]
    public void DefaultPalette_PassesValidation() {
        var result = ThemeContrast.Validate(DefaultPalette(), ThemeChroma.None, StatusTokens);

        Assert.True(result.Passes, string.Join("; ",
            result.Failures.Select(f => $"{f.Check} {f.A}/{f.B} {f.Measured} < {f.Required}")));
    }

    [Fact]
    public void LowContrastTheme_IsRejected() {
        var colors = new Dictionary<string, ThemeColor>(DefaultPalette()) {
            ["bg"] = ThemeColor.FromHex("#808080")!.Value,
            ["fg"] = ThemeColor.FromHex("#8a8a8a")!.Value,
        };

        var result = ThemeContrast.Validate(colors, ThemeChroma.None, StatusTokens);

        Assert.False(result.Passes);
        Assert.Contains(result.Failures, f => f is { Check: "contrast", A: "fg", B: "bg" });
    }

    [Fact]
    public void AccentTooCloseToErr_FailsDistinguishability() {
        var colors = new Dictionary<string, ThemeColor>(DefaultPalette()) {
            ["accent"] = ThemeColor.FromHex("#e0685f")!.Value,
        };

        var result = ThemeContrast.Validate(colors, ThemeChroma.None, StatusTokens);

        Assert.False(result.Passes);
        Assert.Contains(result.Failures, f => f.Check == "distinguish");
    }

    [Fact]
    public void HueRotation_IsJudgedAtTheWorstHue() {
        var colors = DefaultPalette();
        Assert.True(ThemeContrast.Validate(colors, ThemeChroma.None, StatusTokens).Passes);

        var rotatingChroma = new ThemeChroma(HueRotate: new ThemeHueRotate(true, 30)).Clamped();
        var result = ThemeContrast.Validate(colors, rotatingChroma, StatusTokens);

        if (result.Failures.Count == 0) {
            Assert.True(result.Passes);
        } else {
            Assert.All(result.Failures.Where(f => f.A == "accent"), f => Assert.NotNull(f.AtHue));
        }
    }

    [Fact]
    public void FailureRows_CarryMeasurementAndRequirement() {
        var colors = new Dictionary<string, ThemeColor>(DefaultPalette()) {
            ["bg"] = ThemeColor.FromHex("#808080")!.Value,
            ["fg"] = ThemeColor.FromHex("#8a8a8a")!.Value,
        };

        var result = ThemeContrast.Validate(colors, ThemeChroma.None, StatusTokens);

        Assert.False(result.Passes);
        foreach (var failure in result.Failures) {
            Assert.True(failure.Measured < failure.Required);
            Assert.True(failure.Measured > 0);
        }
    }
}
