namespace EggIdentity.Styles.Tests;

public class ComponentTokensTests {
    [Fact]
    public void Required_ContainsAllTenRoleTokensExactlyOnce() {
        Assert.Equal(10, ComponentTokens.Required.Length);
        Assert.Equal(ComponentTokens.Required.Length, ComponentTokens.Required.Distinct().Count());
        Assert.Contains("--color-accent", ComponentTokens.Required);
        Assert.Contains("--color-border", ComponentTokens.Required);
    }
}
