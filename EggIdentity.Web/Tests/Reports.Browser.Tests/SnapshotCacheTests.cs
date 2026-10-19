using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace EggIdentity.Reports.Browser.Tests;

public sealed class SnapshotCacheTests {
    private static readonly IServiceProvider NoServices = new ServiceCollection().BuildServiceProvider();

    private static ReportSnapshotCapture Capture(string source, Func<string?, string> version, Action<string?>? onCapture = null, int size = 3) =>
        new(source,
            (_, p, _) => ValueTask.FromResult(version(p)),
            (_, p, _, _) => {
                onCapture?.Invoke(p);
                return ValueTask.FromResult(new byte[size]);
            },
            null);

    private static ValueTask<(byte[] Bytes, string ETag)> Get(SnapshotCache cache, string scope, string? partition, string source, string version) =>
        cache.GetAsync(scope, partition, source, version, NoServices, CancellationToken.None);

    [Fact]
    public async Task GetAsync_ReusesBytesForSameVersion() {
        var calls = 0;
        var cache = new SnapshotCache([Capture("s1", _ => "v1", _ => calls++)], new BrowserReportOptions(), new FakeTimeProvider());

        var first = await Get(cache, "u1", null, "s1", "v1");
        var second = await Get(cache, "u1", null, "s1", "v1");

        Assert.Equal(1, calls);
        Assert.Same(first.Bytes, second.Bytes);
        Assert.Equal(SnapshotCache.ETagFor("v1"), second.ETag);
    }

    [Fact]
    public async Task GetAsync_RecapturesOnNewVersion() {
        var calls = 0;
        var cache = new SnapshotCache([Capture("s1", _ => "v1", _ => calls++)], new BrowserReportOptions(), new FakeTimeProvider());

        var first = await Get(cache, "u1", null, "s1", "v1");
        var second = await Get(cache, "u1", null, "s1", "v2");

        Assert.Equal(2, calls);
        Assert.NotEqual(first.ETag, second.ETag);
    }

    [Fact]
    public async Task GetAsync_NeverSharesAcrossScopesOrPartitions() {
        var seen = new List<string?>();
        var cache = new SnapshotCache([Capture("s1", _ => "v1", seen.Add)], new BrowserReportOptions(), new FakeTimeProvider());

        var a = await Get(cache, "userA", "acct1", "s1", "v1");
        var b = await Get(cache, "userB", "acct1", "s1", "v1");
        var a2 = await Get(cache, "userA", "acct2", "s1", "v1");

        Assert.Equal(3, seen.Count);
        Assert.NotSame(a.Bytes, b.Bytes);
        Assert.NotSame(a.Bytes, a2.Bytes);
    }

    [Fact]
    public async Task GetAsync_EvictsBeyondByteBound() {
        var options = new BrowserReportOptions { MaxCacheBytes = 20 };
        var cache = new SnapshotCache([Capture("s0", _ => "v1", size: 10), Capture("s1", _ => "v1", size: 10), Capture("s2", _ => "v1", size: 10)], options, new FakeTimeProvider());

        await Get(cache, "u", null, "s0", "v1");
        await Get(cache, "u", null, "s1", "v1");
        await Get(cache, "u", null, "s2", "v1");

        Assert.Null(cache.CachedVersion("u", null, "s0"));
        Assert.NotNull(cache.CachedVersion("u", null, "s1"));
        Assert.NotNull(cache.CachedVersion("u", null, "s2"));
    }

    [Fact]
    public async Task GetAsync_TtlEvictsIdleEntriesOnly() {
        var clock = new FakeTimeProvider();
        var calls = 0;
        var cache = new SnapshotCache([Capture("hot", _ => "v1", _ => calls++), Capture("idle", _ => "v1", _ => calls++)], new BrowserReportOptions { CacheTtl = TimeSpan.FromMinutes(1) }, clock);

        await Get(cache, "u", null, "hot", "v1");
        await Get(cache, "u", null, "idle", "v1");
        clock.Advance(TimeSpan.FromSeconds(45));
        await Get(cache, "u", null, "hot", "v1");
        clock.Advance(TimeSpan.FromSeconds(45));
        await Get(cache, "u", null, "hot", "v1");

        Assert.Equal(2, calls);
        Assert.Null(cache.CachedVersion("u", null, "idle"));
        Assert.NotNull(cache.CachedVersion("u", null, "hot"));
    }

    [Fact]
    public async Task VersionAsync_DoesNotCapture() {
        var calls = 0;
        var cache = new SnapshotCache([Capture("s1", p => $"v-{p}", _ => calls++)], new BrowserReportOptions(), new FakeTimeProvider());

        var version = await cache.VersionAsync("acct", "s1", NoServices, CancellationToken.None);

        Assert.Equal("v-acct", version);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ETagFor_IsQuotedHexForAnyVersion() {
        var etag = SnapshotCache.ETagFor("v\"1 ü");
        Assert.StartsWith("\"", etag, StringComparison.Ordinal);
        Assert.EndsWith("\"", etag, StringComparison.Ordinal);
        Assert.Equal(34, etag.Length);
    }
}
