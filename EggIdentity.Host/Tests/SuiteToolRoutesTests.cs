using EggIdentity.Deploy;

namespace EggIdentity.Host.Tests;

public class SuiteToolRoutesTests {
    private static SuiteApp App(string name, string slug, string url, bool listed = true) => new() {
        Name = name,
        BrandSlug = slug,
        PublicUrl = url,
        Listed = listed,
        Stack = "egg-apps",
        DiscordToken = "secret",
        AuthClientSecret = "secret",
    };

    [Fact]
    public void ProjectsProdEnabledRowsOnlyInRegistryOrder() {
        var tools = SuiteToolRoutes.Project([
            App("eggledger", "ledger", "https://eggledger.egginc.tools/app"),
            App("eggledger-sub", "ledger", "https://sub.eggledger.egginc.tools") with { Environment = SuiteApp.SubProdEnvironment },
            App("eggincognito", "incognito", "https://eggincognito.egginc.tools") with { Enabled = false },
            App("eggabacus", "abacus", "https://eggabacus.egginc.tools", listed: false),
            App("nobrand", "nope", "https://x.egginc.tools"),
            App("plain", "identity", "http://id.egginc.tools"),
        ]);

        Assert.Equal(["ledger", "abacus"], tools.Select(t => t.Slug));
        Assert.Equal("https://eggledger.egginc.tools", tools[0].Url);
        Assert.True(tools[0].Live);
        Assert.False(tools[1].Live);
        Assert.Equal([0, 1], tools.Select(t => t.Order));
    }

    [Fact]
    public void WireShapeCarriesNoPrivateFields() {
        var names = typeof(EggIdentity.Contract.SuiteTool).GetProperties().Select(p => p.Name).Order();
        Assert.Equal(["Live", "Order", "Slug", "Url"], names);
    }
}
