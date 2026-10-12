namespace EggIdentity.DbClone.Tests;

public class EnvironmentSettingsTests {
    private static Func<string, string?> Env(params (string Key, string Value)[] values) =>
        key => values.FirstOrDefault(v => v.Key == key).Value;

    [Fact]
    public void Instance_DefaultsToProdNameInProd() =>
        Assert.Equal("eggledger", EnvironmentSettings.Instance(Env(), "eggledger"));

    [Fact]
    public void Instance_DefaultsToSubProdSuffixInSubProd() =>
        Assert.Equal("eggledger-subprod", EnvironmentSettings.Instance(Env(("APP_ENVIRONMENT", "subprod")), "eggledger"));

    [Fact]
    public void Instance_ExplicitValueWins() =>
        Assert.Equal("ledger-b", EnvironmentSettings.Instance(Env(("APP_ENVIRONMENT", "subprod"), ("APP_INSTANCE", " ledger-b ")), "eggledger"));

    [Fact]
    public void DiscordAllowed_AlwaysInProd() =>
        Assert.True(EnvironmentSettings.DiscordAllowed(Env()));

    [Fact]
    public void DiscordAllowed_FencedInSubProdUntilOpened() {
        Assert.False(EnvironmentSettings.DiscordAllowed(Env(("APP_ENVIRONMENT", "subprod"))));
        Assert.True(EnvironmentSettings.DiscordAllowed(Env(("APP_ENVIRONMENT", "subprod"), ("SUBPROD_ALLOW_DISCORD", "true"))));
    }
}
