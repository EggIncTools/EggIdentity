namespace EggIdentity.Resilience;

public static class Backoff {
    public static TimeSpan Delay(int attempt, RetryOptions options, Random? random = null) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        var cap = Cap(attempt, options);
        if (!options.Jitter) return cap;
        var fraction = (random ?? Random.Shared).NextDouble();
        return TimeSpan.FromTicks((long)(cap.Ticks * fraction));
    }

    public static TimeSpan Cap(int attempt, RetryOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        var exponent = Math.Min(attempt - 1, 30);
        var scaled = options.BaseDelay.Ticks * Math.Pow(2, exponent);
        var capped = Math.Min(scaled, options.MaxDelay.Ticks);
        return TimeSpan.FromTicks((long)capped);
    }
}
