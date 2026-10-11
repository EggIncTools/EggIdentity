using EggIdentity.Contract;
using EggIdentity.UI.Suite;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.UI.Tests;

public class SuiteTests {
    private static DefaultHttpContext Request(string header, string value) {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[header] = value;
        return ctx;
    }

    [Theory]
    [InlineData("Sec-Purpose", "prefetch;prerender", true)]
    [InlineData("Sec-Purpose", "prefetch", true)]
    [InlineData("Purpose", "prefetch", true)]
    [InlineData("Sec-Fetch-Dest", "document", false)]
    public void SpeculativeRequestsAreRecognised(string header, string value, bool expected) =>
        Assert.Equal(expected, Speculation.IsSpeculative(Request(header, value)));

    [Fact]
    public void NoContextIsNotSpeculative() => Assert.False(Speculation.IsSpeculative(null));

    [Fact]
    public void WireListDropsUnknownBrandsAndForeignHostsAndKeepsOrder() {
        var entries = SuiteCatalog.FromWire([
            new SuiteTool { Slug = "incognito", Url = "https://eggincognito.egginc.tools/", Live = true, Order = 1 },
            new SuiteTool { Slug = "ledger", Url = "https://eggledger.egginc.tools", Live = true, Order = 0 },
            new SuiteTool { Slug = "nope", Url = "https://x.egginc.tools", Live = true, Order = 2 },
            new SuiteTool { Slug = "abacus", Url = "https://evil.example", Live = true, Order = 3 },
        ]);

        Assert.Equal(["ledger", "incognito"], entries.Select(e => e.Slug));
        Assert.Equal("https://eggincognito.egginc.tools", entries[1].Url);
    }

    [Fact]
    public void EmptyWireListFallsBackToCompiledCatalog() =>
        Assert.Same(SuiteCatalog.Fallback, SuiteCatalog.FromWire([]));

    [Theory]
    [InlineData("https://egginc.tools/x", true)]
    [InlineData("https://eggledger.egginc.tools/", true)]
    [InlineData("http://localhost:5000/", true)]
    [InlineData("http://eggledger.egginc.tools/", false)]
    [InlineData("https://egginc.tools.evil.example/", false)]
    [InlineData("/relative", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("javascript:alert(1)", false)]
    public void ReturnGateMatchesHub(string url, bool allowed) =>
        Assert.Equal(allowed, SuiteCatalog.IsAllowedReturn(url));

    [Fact]
    public void RejectedReturnFallsBackToHub() =>
        Assert.EndsWith(Uri.EscapeDataString(SuiteCatalog.HubUrl), SuiteCatalog.SignOutUrlFor("https://evil.example/"));

    [Fact]
    public void SpeculationRulesExcludeNothingTheyAreNotGivenAndEscapeMarkup() {
        var rules = SuiteHandoffRules.Build([new SuiteEntry("ledger", "https://eggledger.egginc.tools", true, 0)]);
        Assert.Contains("https://eggledger.egginc.tools/", rules);
        Assert.Contains("moderate", rules);
        Assert.DoesNotContain("<", rules);
    }

    [Fact]
    public void OnlySuiteCookiesTakeTheParentDomain() {
        var options = new ViewerCookieOptions { Domain = ".egginc.tools" }
            .Allow(ViewerCookieOptions.ThemeCookie, TimeSpan.FromDays(1))
            .Allow("app-private", TimeSpan.FromDays(1));

        Assert.Equal(".egginc.tools", options.DomainFor(ViewerCookieOptions.ThemeCookie));
        Assert.Null(options.DomainFor("app-private"));
    }

    [Fact]
    public void DomainDefaultsToHostOnly() {
        var options = new ViewerCookieOptions().Allow(ViewerCookieOptions.EidCookie, TimeSpan.FromDays(1));
        Assert.Null(options.DomainFor(ViewerCookieOptions.EidCookie));
    }

    [Theory]
    [InlineData(SuitePlacement.Bar, "suite-bar")]
    [InlineData(SuitePlacement.Rail, "suite-rail")]
    [InlineData(SuitePlacement.Tabs, "suite-tabs")]
    [InlineData(SuitePlacement.Drawer, "suite-drawer")]
    [InlineData(SuitePlacement.Inline, "suite-inline")]
    public void EveryPlacementHasItsOwnLiteralClass(SuitePlacement placement, string css) =>
        Assert.Equal(css, SuitePlacements.Css(placement));
}
