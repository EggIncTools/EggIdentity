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
    private const string Base = "https://example.test";

    private static (FakeNavigationManager nav, FakeJSRuntime js, PathRouteSync sync) Start(string path, string prefix) {
        var nav = new FakeNavigationManager(Base + "/", Base + path);
        var js = new FakeJSRuntime();
        var sync = new PathRouteSync(nav, js, prefix);
        sync.Start();
        return (nav, js, sync);
    }

    private static (string Method, string Url) History(FakeJSRuntime js) {
        var call = Assert.Single(js.Calls);
        return (call.Identifier, (string)call.Args![2]!);
    }

    [Fact]
    public void Start_SeedsSegmentsIgnoringQueryAndFragment() =>
        Assert.Equal(["Missions", "List", "Home"], Start("/Missions/List/Home?x=1#frag", "/missions").sync.Segments);

    [Fact]
    public void Segments_EmptyAtRoot() => Assert.Empty(Start("/", "/").sync.Segments);

    [Fact]
    public void Push_NoOpsWhenPathMatchesIgnoringTrailingSlashAndQuery() {
        var (_, js, sync) = Start("/missions/list/all", "/missions");
        sync.Push("/missions/list/all/?x=1");
        Assert.Empty(js.Calls);
    }

    [Fact]
    public void Push_WritesHistoryWithoutNavigatingOrRaisingChanged() {
        var (nav, js, sync) = Start("/missions/list/all", "/missions");
        var fired = 0;
        sync.Changed += () => fired++;

        sync.Push("/missions/calendar/home");

        Assert.Equal(("history.pushState", Base + "/missions/calendar/home"), History(js));
        Assert.Empty(nav.Navigations);
        Assert.Equal(["missions", "calendar", "home"], sync.Segments);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void Replace_UsesReplaceState() {
        var (nav, js, sync) = Start("/protos", "/protos");
        sync.Replace("/protos/terms");
        Assert.Equal(("history.replaceState", Base + "/protos/terms"), History(js));
        Assert.Empty(nav.Navigations);
    }

    [Fact]
    public void ExternalNavigationUnderPrefix_UpdatesSegmentsAndRaisesChangedOnce() {
        var (nav, _, sync) = Start("/protos", "/protos");
        var fired = 0;
        sync.Changed += () => fired++;

        nav.External("/protos/terms");

        Assert.Equal(["protos", "terms"], sync.Segments);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void ExternalNavigationOutsidePrefix_IsIgnored() {
        var (nav, _, sync) = Start("/protos", "/protos");
        var fired = 0;
        sync.Changed += () => fired++;

        nav.External("/protosx/terms");

        Assert.Equal(["protos"], sync.Segments);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void FragmentOnlyChange_UpdatesFragmentAndRaisesChanged() {
        var (nav, _, sync) = Start("/protos#a", "/protos");
        var fired = 0;
        sync.Changed += () => fired++;

        nav.External("/protos#b%20c");

        Assert.Equal("b c", sync.Fragment);
        Assert.Equal(["protos"], sync.Segments);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void ExternalNavigationAfterPush_BackToPreviousPathRaisesChanged() {
        var (nav, _, sync) = Start("/protos", "/protos");
        var fired = 0;
        sync.Changed += () => fired++;

        sync.Push("/protos/terms");
        nav.External("/protos");

        Assert.Equal(["protos"], sync.Segments);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Push_FragmentOnly_WritesHistoryAndUpdatesFragment() {
        var (_, js, sync) = Start("/protos#a", "/protos");
        sync.Push("/protos#b");
        Assert.Equal(Base + "/protos#b", History(js).Url);
        Assert.Equal("b", sync.Fragment);
    }

    [Fact]
    public void PushFragment_KeepsPathAndEscapes() {
        var (_, js, sync) = Start("/protos/terms", "/protos");
        sync.PushFragment("b c");
        Assert.Equal(Base + "/protos/terms#b%20c", History(js).Url);
        Assert.Equal("b c", sync.Fragment);
        Assert.Equal(["protos", "terms"], sync.Segments);
    }

    [Fact]
    public void PushFragment_KeepsTheCurrentQuery() {
        var (_, js, sync) = Start("/protos/terms?x=1&y=2#old", "/protos");
        sync.PushFragment("new");
        Assert.Equal(Base + "/protos/terms?x=1&y=2#new", History(js).Url);
    }

    [Fact]
    public void ReplaceFragment_EmptyClearsFragmentWithReplace() {
        var (_, js, sync) = Start("/protos#a", "/protos");
        sync.ReplaceFragment("");
        Assert.Equal(("history.replaceState", Base + "/protos"), History(js));
        Assert.Equal("", sync.Fragment);
    }

    [Fact]
    public void Dispose_StopsListening() {
        var (nav, _, sync) = Start("/protos", "/protos");
        var fired = 0;
        sync.Changed += () => fired++;

        sync.Dispose();
        nav.External("/protos/terms");

        Assert.Equal(0, fired);
    }
}
