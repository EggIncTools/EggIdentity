using System.Net;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.Visits.Tests;

public class VisitRequestTests {
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static (VisitsOptions options, VisitorHasher hasher) Build(bool proxy = false) =>
        (new VisitsOptions("site") { HostedBehindProxy = proxy }, new VisitorHasher(new FixedClock(Start)));

    private static DefaultHttpContext Request(string ua = "Mozilla/5.0") {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers.UserAgent = ua;
        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
        return ctx;
    }

    [Fact]
    public void SameClientHashesToSameVisitor() {
        var (options, hasher) = Build();
        Assert.Equal(VisitRequest.VisitorOf(Request(), options, hasher), VisitRequest.VisitorOf(Request(), options, hasher));
    }

    [Fact]
    public void ProxyHeadersSeparateVisitors() {
        var (options, hasher) = Build(proxy: true);
        var first = Request();
        first.Request.Headers["CF-Connecting-IP"] = "10.0.0.1";
        var second = Request();
        second.Request.Headers["X-Forwarded-For"] = "10.0.0.2, 10.0.0.9";

        Assert.NotEqual(VisitRequest.VisitorOf(first, options, hasher), VisitRequest.VisitorOf(second, options, hasher));
    }

    [Theory]
    [InlineData("DNT", "1")]
    [InlineData("Sec-GPC", "1")]
    [InlineData("Sec-Purpose", "prefetch;prerender")]
    [InlineData("Sec-Fetch-Dest", "iframe")]
    [InlineData("Sec-Fetch-Dest", "image")]
    public void PrivacySignalsExclude(string header, string value) {
        var (options, hasher) = Build();
        var ctx = Request();
        ctx.Request.Headers[header] = value;
        Assert.Null(VisitRequest.VisitorOf(ctx, options, hasher));
    }

    [Theory]
    [InlineData("document")]
    [InlineData("empty")]
    public void NavigationAndFetchDestinationsCount(string dest) {
        var (options, hasher) = Build();
        var ctx = Request();
        ctx.Request.Headers["Sec-Fetch-Dest"] = dest;
        Assert.NotNull(VisitRequest.VisitorOf(ctx, options, hasher));
    }

    [Theory]
    [InlineData("Googlebot/2.1")]
    [InlineData("Mozilla/5.0 (compatible; AhrefsBot)")]
    [InlineData("HeadlessChrome/120")]
    [InlineData("facebookexternalhit preview")]
    public void BotUserAgentsExcluded(string ua) {
        var (options, hasher) = Build();
        Assert.Null(VisitRequest.VisitorOf(Request(ua), options, hasher));
    }

    [Fact]
    public void ConsentDeclineExcludesAndAcceptCounts() {
        var (options, hasher) = Build();
        var declined = Request();
        declined.Request.Headers.Cookie = "eggidentity_consent=v=1&f=1&a=0&t=1";
        Assert.Null(VisitRequest.VisitorOf(declined, options, hasher));

        var accepted = Request();
        accepted.Request.Headers.Cookie = "eggidentity_consent=v%3D1%26f%3D1%26a%3D1%26t%3D1";
        Assert.NotNull(VisitRequest.VisitorOf(accepted, options, hasher));
    }

    [Theory]
    [InlineData("https://app.example.com/docs?x=1#y", "/docs")]
    [InlineData("https://app.example.com", "/")]
    [InlineData("not a uri", "/")]
    public void PathOfDropsQueryAndFragment(string uri, string expected) =>
        Assert.Equal(expected, VisitRequest.PathOf(uri));

    [Fact]
    public void PathOfCapsLength() =>
        Assert.Equal(VisitRequest.MaxPathLength, VisitRequest.PathOf("https://a.example.com/" + new string('x', 2000)).Length);
}
