using EggIdentity.Styles.Theming;

namespace EggIdentity.Styles.Tests;

public class ThemeTokenRegistryTests {
    [Fact]
    public void BaselineSettable_ContainsRequiredAndOptionalComponentTokens() {
        foreach (var token in ComponentTokens.Required) {
            Assert.Contains(StripColorPrefix(token), ThemeTokenRegistry.BaselineSettable);
        }
        foreach (var token in ComponentTokens.Optional) {
            Assert.Contains(StripColorPrefix(token), ThemeTokenRegistry.BaselineSettable);
        }
        Assert.Equal(ComponentTokens.Required.Length + ComponentTokens.Optional.Length, ThemeTokenRegistry.BaselineSettable.Count);
    }

    [Fact]
    public void Register_MakesTokenKnown_OnlyAfterRegistration() {
        var registry = new ThemeTokenRegistry();

        Assert.False(registry.IsKnown("info"));

        registry.Register("info");

        Assert.True(registry.IsKnown("info"));
    }

    [Fact]
    public void IsKnown_ReturnsFalse_ForUnregisteredToken() {
        var registry = new ThemeTokenRegistry();

        Assert.False(registry.IsKnown("view-ships"));
    }

    [Fact]
    public void Canonicalize_ReturnsRegisteredName_ThenNullWhenUnregistered() {
        var registry = new ThemeTokenRegistry();

        registry.Register("info");

        Assert.Equal("info", registry.Canonicalize("info"));
        Assert.Null(registry.Canonicalize("nonexistent"));
        Assert.Equal("accent", registry.Canonicalize("accent"));
    }

    private static string StripColorPrefix(string cssVar) =>
        cssVar.StartsWith("--color-", StringComparison.Ordinal) ? cssVar["--color-".Length..] : cssVar;
}
