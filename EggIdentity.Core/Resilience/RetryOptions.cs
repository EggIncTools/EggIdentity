namespace EggIdentity.Resilience;

public sealed record RetryOptions {
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(10);
    public bool Jitter { get; init; } = true;
    public Func<Exception, bool>? ShouldRetry { get; init; }

    public static RetryOptions Default { get; } = new();
}
