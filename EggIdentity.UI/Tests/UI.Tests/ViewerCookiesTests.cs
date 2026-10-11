using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.UI.Tests;

public class ViewerCookiesTests {
    private static ViewerCookieOptions Options() => new ViewerCookieOptions().Allow("tz", TimeSpan.FromDays(1), ".example.com");

    private static async Task<HttpContext> RunWrite(string query, string? fetchSite = "same-origin") {
        var ctx = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        ctx.Request.QueryString = new QueryString(query);
        if (fetchSite is not null) ctx.Request.Headers["Sec-Fetch-Site"] = fetchSite;
        await ViewerCookieRoutes.Write(ctx, Options()).ExecuteAsync(ctx);
        return ctx;
    }

    [Fact]
    public void SetRejectsUnlistedName() =>
        Assert.Throws<ArgumentException>(() => new ViewerCookies(Options()).Set("other", "x"));

    [Fact]
    public void SetQueuesOneWritePerChange() {
        var cookies = new ViewerCookies(Options());
        cookies.Set("tz", "UTC");
        cookies.Set("tz", "UTC");
        Assert.Single(cookies.Pending);
        cookies.Written("tz", cookies.Pending[0]);
        Assert.Empty(cookies.Pending);
    }

    [Fact]
    public void RepeatedChangesOnlyEverQueueOneWritePerName() {
        var cookies = new ViewerCookies(Options());
        cookies.Set("tz", "A");
        var firstUrl = cookies.Pending[0];
        cookies.Set("tz", "B");
        cookies.Set("tz", "A");

        Assert.Single(cookies.Pending);
        Assert.Equal(firstUrl, cookies.Pending[0]);
    }

    [Fact]
    public void WrittenIgnoresAStaleUrlSupersededByALaterWrite() {
        var cookies = new ViewerCookies(Options());
        cookies.Set("tz", "A");
        var staleUrl = cookies.Pending[0];
        cookies.Set("tz", "B");

        cookies.Written("tz", staleUrl);

        Assert.Single(cookies.Pending);
    }

    [Fact]
    public void RemoveQueuesADeleteOnlyWhenTheCookieWasSet() {
        var cookies = new ViewerCookies(new ViewerCookieOptions().Allow("tz", TimeSpan.FromDays(1)));
        var changed = new List<string>();
        cookies.ValueChanged += changed.Add;

        cookies.Remove("tz");
        Assert.Empty(cookies.Pending);

        cookies.Set("tz", "UTC");
        cookies.Remove("tz");

        Assert.Null(cookies.Get("tz"));
        Assert.Contains("remove=1", cookies.Pending[^1]);
        Assert.Equal(["tz", "tz"], changed);
    }

    [Fact]
    public void ValueChangedNamesTheCookieOnlyOnRealChange() {
        var cookies = new ViewerCookies(Options());
        var names = new List<string>();
        cookies.ValueChanged += names.Add;
        cookies.Set("tz", "UTC");
        cookies.Set("tz", "UTC");
        Assert.Equal(["tz"], names);
    }

    [Fact]
    public void SnapshotReadsOnlyListedRequestCookies() {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.Cookie = "tz=Asia%2FTokyo; other=1";
        var cookies = new ViewerCookies(Options(), new HttpContextAccessor { HttpContext = ctx });
        Assert.Equal("Asia/Tokyo", cookies.Get("tz"));
        Assert.Null(cookies.Get("other"));
    }

    [Fact]
    public async Task WriteSetsCookieWithRuleDomain() {
        var ctx = await RunWrite("?name=tz&value=Asia%2FTokyo");
        Assert.Equal(204, ctx.Response.StatusCode);
        var header = ctx.Response.Headers.SetCookie.ToString();
        Assert.StartsWith("tz=", header);
        Assert.Contains("domain=.example.com", header);
    }

    [Theory]
    [InlineData("?name=other&value=x", "same-origin", 400)]
    [InlineData("?name=tz&value=x", "cross-site", 403)]
    public async Task WriteRejectsUnlistedOrCrossSite(string query, string fetchSite, int status) {
        var ctx = await RunWrite(query, fetchSite);
        Assert.Equal(status, ctx.Response.StatusCode);
        Assert.Empty(ctx.Response.Headers.SetCookie.ToString());
    }
}
