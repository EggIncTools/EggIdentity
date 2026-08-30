using EggIdentity.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggIdentity.UI.Tests;

internal sealed class FakeNavigationManager : NavigationManager {
    public FakeNavigationManager(string baseUri, string uri) {
        Initialize(baseUri, uri);
    }

    protected override void NavigateToCore(string uri, NavigationOptions options) {
        Uri = ToAbsoluteUri(uri).ToString();
        NotifyLocationChanged(false);
    }
}

internal sealed class FakeJSRuntime : IJSRuntime {
    public readonly List<(string Identifier, object?[]? Args)> Calls = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) {
        Calls.Add((identifier, args));
        return ValueTask.FromResult<TValue>(default!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
        Calls.Add((identifier, args));
        return ValueTask.FromResult<TValue>(default!);
    }
}

public class PathRouteSyncTests {
    private static FakeNavigationManager MakeNav(string path) {
        return new FakeNavigationManager("https://example.test/", $"https://example.test{path}");
    }

    [Fact]
    public async Task StartAsync_SeedsSegmentsAndCallsListen() {
        var nav = MakeNav("/Missions/List/Home?x=1#frag");
        var jsRuntime = new FakeJSRuntime();
        var sync = new PathRouteSync(jsRuntime, nav, "/missions");

        await sync.StartAsync();

        Assert.Equal(["Missions", "List", "Home"], sync.Segments);
        var call = Assert.Single(jsRuntime.Calls);
        Assert.Equal("pathRouteSyncListen", call.Identifier);
        Assert.Equal("/missions", call.Args?[0]);
    }

    [Fact]
    public async Task Segments_EmptyAtRoot() {
        var nav = MakeNav("/");
        var jsRuntime = new FakeJSRuntime();
        var sync = new PathRouteSync(jsRuntime, nav, "/");

        await sync.StartAsync();

        Assert.Empty(sync.Segments);
    }

    [Fact]
    public async Task Push_NoOpsWhenPathMatchesIgnoringTrailingSlashAndQuery() {
        var nav = MakeNav("/missions/list/all");
        var jsRuntime = new FakeJSRuntime();
        var sync = new PathRouteSync(jsRuntime, nav, "/missions");
        await sync.StartAsync();
        jsRuntime.Calls.Clear();

        await sync.Push("/missions/list/all/?x=1");

        Assert.Empty(jsRuntime.Calls);
    }

    [Fact]
    public async Task Push_NavigatesWhenPathDiffers() {
        var nav = MakeNav("/missions/list/all");
        var jsRuntime = new FakeJSRuntime();
        var sync = new PathRouteSync(jsRuntime, nav, "/missions");
        await sync.StartAsync();
        jsRuntime.Calls.Clear();

        await sync.Push("/missions/calendar/home");

        var call = Assert.Single(jsRuntime.Calls);
        Assert.Equal("pathRouteSyncPush", call.Identifier);
        Assert.Equal("/missions/calendar/home", call.Args?[0]);
        Assert.False((bool)call.Args![1]!);
        Assert.Equal(["missions", "calendar", "home"], sync.Segments);
    }

    [Fact]
    public async Task Replace_PassesReplaceFlag() {
        var nav = MakeNav("/protos");
        var jsRuntime = new FakeJSRuntime();
        var sync = new PathRouteSync(jsRuntime, nav, "/protos");
        await sync.StartAsync();
        jsRuntime.Calls.Clear();

        await sync.Replace("/protos/terms");

        var call = Assert.Single(jsRuntime.Calls);
        Assert.Equal("pathRouteSyncPush", call.Identifier);
        Assert.True((bool)call.Args![1]!);
    }

    [Fact]
    public async Task OnPathChanged_UpdatesSegmentsAndRaisesChangedOnce() {
        var nav = MakeNav("/protos");
        var jsRuntime = new FakeJSRuntime();
        var sync = new PathRouteSync(jsRuntime, nav, "/protos");
        await sync.StartAsync();
        var fired = 0;
        sync.Changed += () => fired++;

        sync.OnPathChanged("/protos/terms");

        Assert.Equal(["protos", "terms"], sync.Segments);
        Assert.Equal(1, fired);
    }

    [Fact]
    public async Task Push_DoesNotRaiseChanged() {
        var nav = MakeNav("/protos");
        var jsRuntime = new FakeJSRuntime();
        var sync = new PathRouteSync(jsRuntime, nav, "/protos");
        await sync.StartAsync();
        var fired = 0;
        sync.Changed += () => fired++;

        await sync.Push("/protos/terms");

        Assert.Equal(0, fired);
    }

    [Fact]
    public async Task Replace_DoesNotRaiseChanged() {
        var nav = MakeNav("/protos");
        var jsRuntime = new FakeJSRuntime();
        var sync = new PathRouteSync(jsRuntime, nav, "/protos");
        await sync.StartAsync();
        var fired = 0;
        sync.Changed += () => fired++;

        await sync.Replace("/protos/terms");

        Assert.Equal(0, fired);
    }

    [Fact]
    public async Task DisposeAsync_CallsUnlisten() {
        var nav = MakeNav("/protos");
        var jsRuntime = new FakeJSRuntime();
        var sync = new PathRouteSync(jsRuntime, nav, "/protos");
        await sync.StartAsync();
        jsRuntime.Calls.Clear();

        await sync.DisposeAsync();

        var call = Assert.Single(jsRuntime.Calls);
        Assert.Equal("pathRouteSyncUnlisten", call.Identifier);
        Assert.Equal("/protos", call.Args?[0]);
    }
}
