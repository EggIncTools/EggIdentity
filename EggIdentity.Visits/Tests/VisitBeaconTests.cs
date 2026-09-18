using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Visits.Tests;

public class VisitBeaconTests {
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static (VisitsOptions options, VisitorHasher hasher, VisitTracker tracker) Build(bool proxy = false) {
        var clock = new FixedClock(Start);
        var options = new VisitsOptions("site") { HostedBehindProxy = proxy };
        return (options, new VisitorHasher(clock), new VisitTracker(options, clock));
    }

    private static DefaultHttpContext Request(string body, string ua = "Mozilla/5.0") {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        ctx.Request.Headers.UserAgent = ua;
        ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
        return ctx;
    }

    private static async Task<int> StatusOf(IResult result) {
        var ctx = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        await result.ExecuteAsync(ctx);
        return ctx.Response.StatusCode;
    }

    [Fact]
    public async Task CountsValidBeat() {
        var (options, hasher, tracker) = Build();
        var result = await VisitBeacon.HandleAsync(Request("""{"path":"/docs","seconds":0}"""), options, hasher, tracker);

        Assert.Equal(204, await StatusOf(result));
        var day = Assert.Single(tracker.Flush());
        Assert.Equal(1, day.Visitors);
        Assert.Equal(1, day.Paths["/docs"]);
    }

    [Fact]
    public async Task SameClientIsOneVisitorAcrossBeats() {
        var (options, hasher, tracker) = Build();
        await VisitBeacon.HandleAsync(Request("""{"path":"/","seconds":0}"""), options, hasher, tracker);
        await VisitBeacon.HandleAsync(Request("""{"path":"/","seconds":12}"""), options, hasher, tracker);

        var day = Assert.Single(tracker.Flush());
        Assert.Equal(1, day.Visitors);
        Assert.Equal(12, day.DurationSeconds);
    }

    [Fact]
    public async Task ProxyHeadersSeparateVisitors() {
        var (options, hasher, tracker) = Build(proxy: true);
        var first = Request("""{"path":"/","seconds":0}""");
        first.Request.Headers["CF-Connecting-IP"] = "10.0.0.1";
        var second = Request("""{"path":"/","seconds":0}""");
        second.Request.Headers["X-Forwarded-For"] = "10.0.0.2, 10.0.0.9";
        await VisitBeacon.HandleAsync(first, options, hasher, tracker);
        await VisitBeacon.HandleAsync(second, options, hasher, tracker);

        Assert.Equal(2, Assert.Single(tracker.Flush()).Visitors);
    }

    [Theory]
    [InlineData("DNT", "1")]
    [InlineData("Sec-GPC", "1")]
    public async Task PrivacySignalsExclude(string header, string value) {
        var (options, hasher, tracker) = Build();
        var ctx = Request("""{"path":"/","seconds":0}""");
        ctx.Request.Headers[header] = value;

        Assert.Equal(204, await StatusOf(await VisitBeacon.HandleAsync(ctx, options, hasher, tracker)));
        Assert.Empty(tracker.Flush());
    }

    [Theory]
    [InlineData("Googlebot/2.1")]
    [InlineData("Mozilla/5.0 (compatible; AhrefsBot)")]
    [InlineData("HeadlessChrome/120")]
    [InlineData("facebookexternalhit preview")]
    public async Task BotUserAgentsExcluded(string ua) {
        var (options, hasher, tracker) = Build();
        await VisitBeacon.HandleAsync(Request("""{"path":"/","seconds":0}""", ua), options, hasher, tracker);
        Assert.Empty(tracker.Flush());
    }

    [Fact]
    public async Task ConsentDeclineExcludesAndAcceptCounts() {
        var (options, hasher, tracker) = Build();
        var declined = Request("""{"path":"/","seconds":0}""");
        declined.Request.Headers.Cookie = "eggidentity_consent=v=1&f=1&a=0&t=1";
        await VisitBeacon.HandleAsync(declined, options, hasher, tracker);
        Assert.Empty(tracker.Flush());

        var accepted = Request("""{"path":"/","seconds":0}""");
        accepted.Request.Headers.Cookie = "eggidentity_consent=v%3D1%26f%3D1%26a%3D1%26t%3D1";
        await VisitBeacon.HandleAsync(accepted, options, hasher, tracker);
        Assert.Single(tracker.Flush());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"path":"","seconds":0}""")]
    [InlineData("""{"path":"docs","seconds":0}""")]
    [InlineData("""{"seconds":0}""")]
    public async Task RejectsMalformedBeats(string body) {
        var (options, hasher, tracker) = Build();
        Assert.Equal(400, await StatusOf(await VisitBeacon.HandleAsync(Request(body), options, hasher, tracker)));
        Assert.Empty(tracker.Flush());
    }

    [Fact]
    public async Task ClampsSecondsToSessionGap() {
        var (options, hasher, tracker) = Build();
        await VisitBeacon.HandleAsync(Request("""{"path":"/","seconds":99999}"""), options, hasher, tracker);
        Assert.Equal((long)options.SessionGap.TotalSeconds, Assert.Single(tracker.Flush()).DurationSeconds);
    }
}
