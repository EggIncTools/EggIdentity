namespace EggIdentity.Resilience.Tests;

public class DeadlineTests {
    [Fact]
    public async Task RunAsync_CompletesInTime_ReturnsResult() {
        var result = await Deadline.RunAsync("fast", _ => Task.FromResult(5), TimeSpan.FromSeconds(1), new FakeTimeProvider());
        Assert.Equal(5, result);
    }

    [Fact]
    public async Task RunAsync_ExceedsTimeout_ThrowsTimeoutWithOperationName() {
        var time = new FakeTimeProvider();
        var task = Deadline.RunAsync("inspect container", async token => {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reg = token.Register(() => tcs.TrySetCanceled(token));
            return await tcs.Task;
        }, TimeSpan.FromSeconds(30), time);

        Assert.False(task.IsCompleted);
        time.Advance(TimeSpan.FromSeconds(30));

        var e = await Assert.ThrowsAsync<TimeoutException>(() => task);
        Assert.Contains("inspect container", e.Message);
        Assert.Contains("30s", e.Message);
    }

    [Fact]
    public async Task RunAsync_CallerCancels_PropagatesCancellationNotTimeout() {
        using var cts = new CancellationTokenSource();
        var task = Deadline.RunAsync("wait", async token => {
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reg = token.Register(() => tcs.TrySetCanceled(token));
            return await tcs.Task;
        }, TimeSpan.FromSeconds(30), new FakeTimeProvider(), cts.Token);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task RunAsync_NonGeneric_TimesOut() {
        var time = new FakeTimeProvider();
        var task = Deadline.RunAsync("pull", async token => {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reg = token.Register(() => tcs.TrySetCanceled(token));
            await tcs.Task;
        }, TimeSpan.FromMinutes(10), time);

        time.Advance(TimeSpan.FromMinutes(10));

        await Assert.ThrowsAsync<TimeoutException>(() => task);
    }
}
