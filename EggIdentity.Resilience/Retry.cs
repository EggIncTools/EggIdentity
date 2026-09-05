namespace EggIdentity.Resilience;

public static class Retry {
    public static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> op, RetryOptions options, TimeProvider? time = null, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(op);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxAttempts, 1);

        var clock = time ?? TimeProvider.System;
        for (var attempt = 1; ; attempt++) {
            ct.ThrowIfCancellationRequested();
            try {
                return await op(ct).ConfigureAwait(false);
            } catch (Exception e) when (attempt < options.MaxAttempts && IsRetryable(e, options, ct)) {
                await Task.Delay(Backoff.Delay(attempt, options), clock, ct).ConfigureAwait(false);
            }
        }
    }

    public static async Task RunAsync(
        Func<CancellationToken, Task> op, RetryOptions options, TimeProvider? time = null, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(op);
        await RunAsync(async token => {
            await op(token).ConfigureAwait(false);
            return true;
        }, options, time, ct).ConfigureAwait(false);
    }

    private static bool IsRetryable(Exception e, RetryOptions options, CancellationToken ct) {
        if (e is OperationCanceledException && ct.IsCancellationRequested) return false;
        return options.ShouldRetry?.Invoke(e) ?? e is not OperationCanceledException;
    }
}
