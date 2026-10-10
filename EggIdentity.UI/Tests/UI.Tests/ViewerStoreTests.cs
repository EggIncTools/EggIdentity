namespace EggIdentity.UI.Tests;

public class ViewerStoreTests {
    private sealed record Prefs(List<string> History);

    private static ViewerCookies Cookies() => new(new ViewerCookieOptions().Allow("prefs", TimeSpan.FromDays(1)));

    [Fact]
    public void RoundTripsThroughTheCookie() {
        var cookies = Cookies();
        var store = new ViewerStore<Prefs>(cookies, "prefs", () => new Prefs([]));

        Assert.True(store.Save(new Prefs(["a", "b"])));

        Assert.Equal(["a", "b"], new ViewerStore<Prefs>(cookies, "prefs", () => new Prefs([])).Load().History);
    }

    [Fact]
    public void MissingOrCorruptCookieYieldsEmpty() {
        var cookies = Cookies();
        var store = new ViewerStore<Prefs>(cookies, "prefs", () => new Prefs(["default"]));
        Assert.Equal(["default"], store.Load().History);

        cookies.Set("prefs", "{not json");
        Assert.Equal(["default"], store.Load().History);
    }

    [Fact]
    public void TrimsUntilItFitsAndRefusesWhenItCannot() {
        var cookies = Cookies();
        var big = new Prefs([.. Enumerable.Range(0, 2000).Select(i => $"entry-{i:0000}")]);

        var untrimmable = new ViewerStore<Prefs>(cookies, "prefs", () => new Prefs([]));
        Assert.False(untrimmable.Save(big));
        Assert.Null(cookies.Get("prefs"));

        var trimming = new ViewerStore<Prefs>(cookies, "prefs", () => new Prefs([]), p => new Prefs(p.History[1..]));
        Assert.True(trimming.Save(big));
        var kept = trimming.Load().History;
        Assert.True(kept.Count < 2000);
        Assert.Equal("entry-1999", kept[^1]);
    }
}
