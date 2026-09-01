using EggIdentity.Styles.Theming;

namespace EggIdentity.Styles.Tests;

public class ThemeDerivedTokensTests {
    [Fact]
    public void GlowValue_FormatsRadiusAndAlpha() {
        var css = ThemeDerivedTokens.GlowValue(12, 30);

        Assert.Equal("0 0 12px color-mix(in oklab, var(--color-accent) 30%, transparent),", css);
    }

    [Fact]
    public void PanelTintValue_FormatsTintPercent() {
        var css = ThemeDerivedTokens.PanelTintValue(6);

        Assert.Equal("color-mix(in oklab, var(--color-panel), var(--color-accent) 6%)", css);
    }
}
