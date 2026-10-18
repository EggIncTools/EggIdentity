using System.Net;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.SubProd.Tests;

public class SubProdSignInTests {
    private static Func<string, string?> Env(params (string Key, string Value)[] pairs) {
        var map = pairs.ToDictionary(p => p.Key, p => p.Value);
        return key => map.GetValueOrDefault(key);
    }

    private static DefaultHttpContext Request(IPAddress remote, string host = "localhost") {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = remote;
        ctx.Request.Host = new HostString(host, 5000);
        return ctx;
    }

    [Fact]
    public void DisabledWithoutGate() =>
        Assert.False(SubProdSignIn.Enabled(Env(("APP_ENVIRONMENT", "subprod"))));

    [Fact]
    public void EnabledInSubProdWithGate() =>
        Assert.True(SubProdSignIn.Enabled(Env(("APP_ENVIRONMENT", "subprod"), ("SUBPROD_ALLOW_SIGNIN", "true"))));

    [Fact]
    public void GateOutsideSubProdThrows() =>
        Assert.Throws<InvalidOperationException>(() => SubProdSignIn.Enabled(Env(("SUBPROD_ALLOW_SIGNIN", "true"))));

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/missions?x=1", "/missions?x=1")]
    [InlineData(null, "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("https://evil.example/", "/")]
    public void ReturnUrlStaysLocal(string? input, string expected) =>
        Assert.Equal(expected, SubProdSignIn.LocalPath(input));

    [Fact]
    public void LoopbackAccepted() {
        Assert.True(SubProdSignIn.IsLoopback(Request(IPAddress.Loopback)));
        Assert.True(SubProdSignIn.IsLoopback(Request(IPAddress.IPv6Loopback, "127.0.0.1")));
    }

    [Fact]
    public void RemoteRejected() =>
        Assert.False(SubProdSignIn.IsLoopback(Request(IPAddress.Parse("10.0.0.5"))));

    [Fact]
    public void PublicHostRejected() =>
        Assert.False(SubProdSignIn.IsLoopback(Request(IPAddress.Loopback, "ledger-subprod.example")));

    [Fact]
    public void ProxiedRequestRejected() {
        var ctx = Request(IPAddress.Loopback);
        ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.9";
        Assert.False(SubProdSignIn.IsLoopback(ctx));
    }
}
