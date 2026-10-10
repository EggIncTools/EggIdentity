namespace EggIdentity.Resilience.Tests;

public class TtlSnapshotTests {
    [Fact]
    public async Task ServesCachedValueUntilTtlThenReloads() {
        var time = new FakeTimeProvider();
        var loads = 0;
        var snap = new TtlSnapshot<int>(TimeSpan.FromMinutes(1), _ => Task.FromResult(++loads), time);

        Assert.Equal(1, await snap.GetAsync());
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(1, await snap.GetAsync());
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, await snap.GetAsync());
    }

    [Fact]
    public async Task InvalidateForcesTheNextReload() {
        var loads = 0;
        var snap = new TtlSnapshot<int>(TimeSpan.FromHours(1), _ => Task.FromResult(++loads), new FakeTimeProvider());

        await snap.GetAsync();
        snap.Invalidate();

        Assert.Equal(2, await snap.GetAsync());
    }

    [Fact]
    public async Task FailedReloadServesStaleAndBacksOffForATtl() {
        var time = new FakeTimeProvider();
        var errors = new List<Exception>();
        var loads = 0;
        var snap = new TtlSnapshot<int>(TimeSpan.FromMinutes(1), _ => ++loads == 1 ? Task.FromResult(7) : throw new InvalidOperationException("down"), time, errors.Add);

        await snap.GetAsync();
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(7, await snap.GetAsync());
        Assert.Equal(7, await snap.GetAsync());
        Assert.Single(errors);
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task FirstLoadFailureThrows() {
        var snap = new TtlSnapshot<int>(TimeSpan.FromMinutes(1), _ => throw new InvalidOperationException("down"), new FakeTimeProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() => snap.GetAsync());
        Assert.False(snap.HasValue);
    }

    [Fact]
    public void SyncLoaderServesCachedThenStaleOnFailure() {
        var time = new FakeTimeProvider();
        var errors = new List<Exception>();
        var loads = 0;
        using var snap = new TtlSnapshot<int>(TimeSpan.FromMinutes(1), () => ++loads == 1 ? 7 : throw new InvalidOperationException("down"), time, errors.Add);

        Assert.Equal(7, snap.Get());
        Assert.Equal(7, snap.Get());
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(7, snap.Get());
        Assert.Single(errors);
        Assert.Equal(2, loads);
    }

    [Fact]
    public void FirstLoadFailureWithoutFallbackRetriesOnTheNextCall() {
        var loads = 0;
        using var snap = new TtlSnapshot<int>(TimeSpan.FromMinutes(1), () => ++loads == 1 ? throw new InvalidOperationException("down") : 5, new FakeTimeProvider());

        Assert.Throws<InvalidOperationException>(() => snap.Get());
        Assert.Equal(5, snap.Get());
        Assert.Equal(2, loads);
    }

    [Fact]
    public void FirstLoadFailureWithFallbackServesItAndBacksOffATtl() {
        var time = new FakeTimeProvider();
        var errors = new List<Exception>();
        var loads = 0;
        using var snap = new TtlSnapshot<int>(TimeSpan.FromMinutes(1), () => ++loads < 3 ? throw new InvalidOperationException("down") : 9, time, errors.Add) { Fallback = -1 };

        Assert.Equal(-1, snap.Get());
        Assert.Equal(-1, snap.Get());
        Assert.Equal(1, loads);
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(-1, snap.Get());
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(9, snap.Get());
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void SyncGetOnAsyncLoaderThrows() {
        using var snap = new TtlSnapshot<int>(TimeSpan.FromMinutes(1), _ => Task.FromResult(1), new FakeTimeProvider());

        Assert.Throws<InvalidOperationException>(() => snap.Get());
    }

    [Fact]
    public async Task ConcurrentCallersShareOneLoad() {
        var loads = 0;
        var gate = new TaskCompletionSource<int>();
        var snap = new TtlSnapshot<int>(TimeSpan.FromMinutes(1), _ => {
            Interlocked.Increment(ref loads);
            return gate.Task;
        }, new FakeTimeProvider());

        var first = snap.GetAsync();
        var second = snap.GetAsync();
        gate.SetResult(3);

        var values = await Task.WhenAll(first, second);
        Assert.All(values, v => Assert.Equal(3, v));
        Assert.Equal(1, loads);
    }
}
