namespace EggIdentity.Resilience;

public static class Deadline {
    public static async Task<T> RunAsync<T>(
        string operation, Func<CancellationToken, Task<T>> op, TimeSpan timeout,
        TimeProvider? time = null, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(op);

        using var timeoutCts = new CancellationTokenSource(timeout, time ?? TimeProvider.System);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        try {
            return await op(linked.Token).ConfigureAwait(false);
        } catch (OperationCanceledException e) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested) {
            throw new TimeoutException($"{operation} timed out after {timeout.TotalSeconds:0.###}s", e);
        }
    }

    public static async Task RunAsync(
        string operation, Func<CancellationToken, Task> op, TimeSpan timeout,
        TimeProvider? time = null, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(op);
        await RunAsync(operation, async token => {
            await op(token).ConfigureAwait(false);
            return true;
        }, timeout, time, ct).ConfigureAwait(false);
    }
}
