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
