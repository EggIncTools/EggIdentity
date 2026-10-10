using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggIdentity.UI.Tests;

internal sealed class FakeNavigationManager : NavigationManager {
    public readonly List<(string Uri, bool Replace)> Navigations = [];

    public FakeNavigationManager(string baseUri, string uri) {
        Initialize(baseUri, uri);
    }

    public void External(string uri) {
        Uri = ToAbsoluteUri(uri).ToString();
        NotifyLocationChanged(false);
    }

    protected override void NavigateToCore(string uri, NavigationOptions options) {
        Navigations.Add((uri, options.ReplaceHistoryEntry));
        Uri = ToAbsoluteUri(uri).ToString();
        NotifyLocationChanged(false);
    }
}

internal sealed class FakeJSRuntime(object? result = null) : IJSRuntime {
    public readonly List<(string Identifier, object?[]? Args)> Calls = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) {
        Calls.Add((identifier, args));
        return ValueTask.FromResult(result is TValue value ? value : default!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
        Calls.Add((identifier, args));
        return ValueTask.FromResult(result is TValue value ? value : default!);
    }
}

public class PathRouteSyncTests {
    private static (FakeNavigationManager nav, PathRouteSync sync) Start(string path, string prefix) {
        var nav = new FakeNavigationManager("https://example.test/", $"https://example.test{path}");
        var sync = new PathRouteSync(nav, prefix);
        sync.Start();
        return (nav, sync);
    }

    [Fact]
    public void Start_SeedsSegmentsIgnoringQueryAndFragment() =>
        Assert.Equal(["Missions", "List", "Home"], Start("/Missions/List/Home?x=1#frag", "/missions").sync.Segments);

    [Fact]
    public void Segments_EmptyAtRoot() => Assert.Empty(Start("/", "/").sync.Segments);

    [Fact]
    public void Push_NoOpsWhenPathMatchesIgnoringTrailingSlashAndQuery() {
        var (nav, sync) = Start("/missions/list/all", "/missions");
        sync.Push("/missions/list/all/?x=1");
        Assert.Empty(nav.Navigations);
    }

    [Fact]
    public void Push_NavigatesWithoutRaisingChanged() {
        var (nav, sync) = Start("/missions/list/all", "/missions");
        var fired = 0;
        sync.Changed += () => fired++;

        sync.Push("/missions/calendar/home");

        Assert.Equal(("/missions/calendar/home", false), Assert.Single(nav.Navigations));
        Assert.Equal(["missions", "calendar", "home"], sync.Segments);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Replace_PassesReplaceFlag() {
        var (nav, sync) = Start("/protos", "/protos");
        sync.Replace("/protos/terms");
        Assert.True(Assert.Single(nav.Navigations).Replace);
    }

    [Fact]
    public void ExternalNavigationUnderPrefix_UpdatesSegmentsAndRaisesChangedOnce() {
        var (nav, sync) = Start("/protos", "/protos");
        var fired = 0;
        sync.Changed += () => fired++;

        nav.External("/protos/terms");

        Assert.Equal(["protos", "terms"], sync.Segments);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void ExternalNavigationOutsidePrefix_IsIgnored() {
        var (nav, sync) = Start("/protos", "/protos");
        var fired = 0;
        sync.Changed += () => fired++;

        nav.External("/protosx/terms");

        Assert.Equal(["protos"], sync.Segments);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void FragmentOnlyChange_UpdatesFragmentAndRaisesChanged() {
        var (nav, sync) = Start("/protos#a", "/protos");
        var fired = 0;
        sync.Changed += () => fired++;

        nav.External("/protos#b%20c");

        Assert.Equal("b c", sync.Fragment);
        Assert.Equal(["protos"], sync.Segments);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Push_FragmentOnly_NavigatesAndUpdatesFragment() {
        var (nav, sync) = Start("/protos#a", "/protos");
        sync.Push("/protos#b");
        Assert.Equal("/protos#b", Assert.Single(nav.Navigations).Uri);
        Assert.Equal("b", sync.Fragment);
    }

    [Fact]
    public void PushFragment_KeepsPathAndEscapes() {
        var (nav, sync) = Start("/protos/terms", "/protos");
        sync.PushFragment("b c");
        Assert.Equal("/protos/terms#b%20c", Assert.Single(nav.Navigations).Uri);
        Assert.Equal("b c", sync.Fragment);
        Assert.Equal(["protos", "terms"], sync.Segments);
    }

    [Fact]
    public void ReplaceFragment_EmptyClearsFragmentWithReplace() {
        var (nav, sync) = Start("/protos#a", "/protos");
        sync.ReplaceFragment("");
        Assert.Equal(("/protos", true), Assert.Single(nav.Navigations));
        Assert.Equal("", sync.Fragment);
    }

    [Fact]
    public void Dispose_StopsListening() {
        var (nav, sync) = Start("/protos", "/protos");
        var fired = 0;
        sync.Changed += () => fired++;

        sync.Dispose();
        nav.External("/protos/terms");

        Assert.Equal(0, fired);
    }
}
