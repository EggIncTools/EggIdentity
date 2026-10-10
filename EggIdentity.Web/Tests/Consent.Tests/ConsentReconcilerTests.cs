namespace EggIdentity.Consent.Tests;

public class ConsentReconcilerTests {
    private static ConsentState At(long unix, bool functional = true) =>
        new(functional, false, 1, DateTimeOffset.FromUnixTimeSeconds(unix));

    [Fact]
    public void BothAbsent_NoWinnerNoWrites() {
        var result = ConsentReconciler.Reconcile(null, null);

        Assert.Null(result.Winner);
        Assert.False(result.WriteCookie);
        Assert.False(result.WriteServer);
    }

    [Fact]
    public void CookieOnly_WritesServer() {
        var cookie = At(100);

        var result = ConsentReconciler.Reconcile(cookie, null);

        Assert.Equal(cookie, result.Winner);
        Assert.False(result.WriteCookie);
        Assert.True(result.WriteServer);
    }

    [Fact]
    public void ServerOnly_WritesCookie() {
        var server = At(100);

        var result = ConsentReconciler.Reconcile(null, server);

        Assert.Equal(server, result.Winner);
        Assert.True(result.WriteCookie);
        Assert.False(result.WriteServer);
    }

    [Fact]
    public void ServerNewer_WinsAndWritesCookie() {
        var result = ConsentReconciler.Reconcile(At(100, functional: false), At(200));

        Assert.True(result.Winner!.Functional);
        Assert.True(result.WriteCookie);
        Assert.False(result.WriteServer);
    }

    [Fact]
    public void CookieNewer_WinsAndWritesServer() {
        var result = ConsentReconciler.Reconcile(At(300), At(200, functional: false));

        Assert.True(result.Winner!.Functional);
        Assert.False(result.WriteCookie);
        Assert.True(result.WriteServer);
    }

    [Fact]
    public void SameInstant_NoWrites() {
        var result = ConsentReconciler.Reconcile(At(100), At(100));

        Assert.NotNull(result.Winner);
        Assert.False(result.WriteCookie);
        Assert.False(result.WriteServer);
    }
}
