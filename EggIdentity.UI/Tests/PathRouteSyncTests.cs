using EggIdentity.UI;
using Microsoft.AspNetCore.Components;

namespace EggIdentity.UI.Tests;

internal sealed class FakeNavigationManager : NavigationManager {
    public readonly List<(string Uri, bool Replace)> Calls = [];

    public FakeNavigationManager(string baseUri, string uri) {
        Initialize(baseUri, uri);
    }

    protected override void NavigateToCore(string uri, NavigationOptions options) {
        Calls.Add((uri, options.ReplaceHistoryEntry));
        Uri = ToAbsoluteUri(uri).ToString();
        NotifyLocationChanged(false);
    }
}

public class PathRouteSyncTests {
    private static FakeNavigationManager MakeNav(string path) {
        return new FakeNavigationManager("https://example.test/", $"https://example.test{path}");
    }

    [Fact]
    public void Segments_SplitsAndDropsEmptyAndPreservesCase() {
        var nav = MakeNav("/Missions/List/Home?x=1#frag");
        var sync = new PathRouteSync(nav);

        Assert.Equal(["Missions", "List", "Home"], sync.Segments);
    }

    [Fact]
    public void Segments_EmptyAtRoot() {
        var nav = MakeNav("/");
        var sync = new PathRouteSync(nav);

        Assert.Empty(sync.Segments);
    }

    [Fact]
    public void Push_NoOpsWhenPathMatchesIgnoringTrailingSlashAndQuery() {
        var nav = MakeNav("/missions/list/all");
        var sync = new PathRouteSync(nav);

        sync.Push("/missions/list/all/?x=1");

        Assert.Empty(nav.Calls);
    }

    [Fact]
    public void Push_NavigatesWhenPathDiffers() {
        var nav = MakeNav("/missions/list/all");
        var sync = new PathRouteSync(nav);

        sync.Push("/missions/calendar/home");

        var call = Assert.Single(nav.Calls);
        Assert.Equal("/missions/calendar/home", call.Uri);
        Assert.False(call.Replace);
    }

    [Fact]
    public void Replace_PassesReplaceFlag() {
        var nav = MakeNav("/protos");
        var sync = new PathRouteSync(nav);

        sync.Replace("/protos/terms");

        var call = Assert.Single(nav.Calls);
        Assert.True(call.Replace);
    }

    [Fact]
    public void Changed_FiresOnLocationChanged() {
        var nav = MakeNav("/protos");
        var sync = new PathRouteSync(nav);
        var fired = 0;
        sync.Changed += () => fired++;

        sync.Push("/protos/terms");

        Assert.Equal(1, fired);
    }

    [Fact]
    public void Dispose_UnsubscribesFromLocationChanged() {
        var nav = MakeNav("/protos");
        var sync = new PathRouteSync(nav);
        var fired = 0;
        sync.Changed += () => fired++;

        sync.Dispose();
        sync.Push("/protos/terms");

        Assert.Equal(0, fired);
    }
}
