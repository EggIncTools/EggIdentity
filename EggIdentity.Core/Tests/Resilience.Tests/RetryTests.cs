namespace EggIdentity.Resilience.Tests;

public class RetryTests {
    private static readonly RetryOptions Options = new() {
        MaxAttempts = 3,
        BaseDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(4),
        Jitter = false,
    };

    private static async Task DriveAsync(FakeTimeProvider time, Task task) {
        for (var spins = 0; spins < 100_000 && !task.IsCompleted; spins++) {
            if (time.PendingTimers > 0) time.Advance(TimeSpan.FromSeconds(1));
            else await Task.Yield();
        }
    }

    [Fact]
    public async Task RunAsync_FailsThenSucceeds_RetriesAfterDelay() {
        var time = new FakeTimeProvider();
        var calls = 0;
        var task = Retry.RunAsync(_ => {
            calls++;
            return calls < 3 ? throw new InvalidOperationException("boom") : Task.FromResult("ok");
        }, Options, time);

        Assert.False(task.IsCompleted);
        Assert.Equal(1, calls);

        await DriveAsync(time, task);

        Assert.Equal("ok", await task);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task RunAsync_AlwaysFails_ThrowsAfterMaxAttempts() {
        var time = new FakeTimeProvider();
        var calls = 0;
        var task = Retry.RunAsync<int>(_ => {
            calls++;
            throw new InvalidOperationException("boom " + calls);
        }, Options, time);

        await DriveAsync(time, task);

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("boom 3", e.Message);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task RunAsync_ShouldRetryFalse_DoesNotRetry() {
        var calls = 0;
        var options = Options with { ShouldRetry = e => e is not ArgumentException };
        await Assert.ThrowsAsync<ArgumentException>(() => Retry.RunAsync<int>(_ => {
            calls++;
            throw new ArgumentException("fatal");
        }, options, new FakeTimeProvider()));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RunAsync_CallerCancelled_DoesNotRetry() {
        using var cts = new CancellationTokenSource();
        var calls = 0;
        var task = Retry.RunAsync<int>(token => {
            calls++;
            cts.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(1);
        }, Options, new FakeTimeProvider(), cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RunAsync_NonGeneric_RetriesToo() {
        var time = new FakeTimeProvider();
        var calls = 0;
        var task = Retry.RunAsync(_ => {
            calls++;
            return calls < 2 ? throw new IOException("flaky") : Task.CompletedTask;
        }, Options, time);

        await DriveAsync(time, task);
        await task;

        Assert.Equal(2, calls);
    }
}
