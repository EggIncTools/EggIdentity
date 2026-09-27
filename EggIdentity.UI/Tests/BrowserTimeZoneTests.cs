namespace EggIdentity.UI.Tests;

public class BrowserTimeZoneTests {
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public void Resolve_UnknownOrBlank_ReturnsNull(string? id) => Assert.Null(BrowserTimeZone.Resolve(id));

    [Fact]
    public void Zone_DefaultsToUtcWithoutRequest() => Assert.Equal(TimeZoneInfo.Utc, new BrowserTimeZone(new FakeJSRuntime()).Zone);

    [Fact]
    public void Apply_RaisesChangedOnlyWhenZoneDiffers() {
        var tz = new BrowserTimeZone(new FakeJSRuntime());
        var raised = 0;
        tz.Changed += () => raised++;

        tz.Apply("America/New_York");
        tz.Apply("America/New_York");
        tz.Apply("garbage");

        Assert.Equal(1, raised);
        Assert.Equal("America/New_York", tz.Zone.Id);
    }

    [Fact]
    public async Task SyncAsync_UsesBrowserZoneWithoutWritingCookie() {
        var js = new FakeJSRuntime("Europe/Berlin");
        var tz = new BrowserTimeZone(js);

        await tz.SyncAsync();

        Assert.Equal("Europe/Berlin", tz.Zone.Id);
        Assert.Equal("eggTimeZoneBrowser", Assert.Single(js.Calls).Identifier);
    }

    [Fact]
    public async Task SyncAsync_PreferredZoneWinsAndIsWritten() {
        var js = new FakeJSRuntime("Europe/Berlin");
        var tz = new BrowserTimeZone(js);

        await tz.SyncAsync("Asia/Tokyo");

        Assert.Equal("Asia/Tokyo", tz.Zone.Id);
        var (identifier, args) = Assert.Single(js.Calls);
        Assert.Equal("eggTimeZoneWrite", identifier);
        Assert.Equal(["Asia/Tokyo"], args);
    }
}
