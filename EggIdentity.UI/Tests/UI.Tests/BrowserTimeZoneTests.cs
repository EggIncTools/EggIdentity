namespace EggIdentity.UI.Tests;

public class BrowserTimeZoneTests {
    private static ViewerCookies Cookies() =>
        new(new ViewerCookieOptions().Allow(BrowserTimeZone.CookieName, TimeSpan.FromDays(1)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public void Resolve_UnknownOrBlank_ReturnsNull(string? id) => Assert.Null(BrowserTimeZone.Resolve(id));

    [Fact]
    public void Zone_DefaultsToUtcWithoutCookie() => Assert.Equal(TimeZoneInfo.Utc, new BrowserTimeZone(new FakeJSRuntime(), Cookies()).Zone);

    [Fact]
    public void Apply_RaisesChangedOnlyWhenZoneDiffers() {
        var tz = new BrowserTimeZone(new FakeJSRuntime(), Cookies());
        var raised = 0;
        tz.Changed += () => raised++;

        tz.Apply("America/New_York");
        tz.Apply("America/New_York");
        tz.Apply("garbage");

        Assert.Equal(1, raised);
        Assert.Equal("America/New_York", tz.Zone.Id);
    }

    [Fact]
    public async Task SyncAsync_BrowserZoneIsReadAndQueuedForCookie() {
        var js = new FakeJSRuntime("Europe/Berlin");
        var cookies = Cookies();
        var tz = new BrowserTimeZone(js, cookies);

        await tz.SyncAsync();

        Assert.Equal("Europe/Berlin", tz.Zone.Id);
        Assert.Equal("eggTimeZoneBrowser", Assert.Single(js.Calls).Identifier);
        Assert.Equal("Europe/Berlin", cookies.Get(BrowserTimeZone.CookieName));
        Assert.Contains("Europe%2FBerlin", Assert.Single(cookies.Pending));
    }

    [Fact]
    public async Task SyncAsync_PreferredZoneWinsWithoutBrowserCall() {
        var js = new FakeJSRuntime("Europe/Berlin");
        var cookies = Cookies();
        var tz = new BrowserTimeZone(js, cookies);

        await tz.SyncAsync("Asia/Tokyo");

        Assert.Equal("Asia/Tokyo", tz.Zone.Id);
        Assert.Empty(js.Calls);
        Assert.Equal("Asia/Tokyo", cookies.Get(BrowserTimeZone.CookieName));
    }
}
