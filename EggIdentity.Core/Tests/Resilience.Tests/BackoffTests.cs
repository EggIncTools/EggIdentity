namespace EggIdentity.Resilience.Tests;

public class BackoffTests {
    private static readonly RetryOptions NoJitter = new() {
        BaseDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(10),
        Jitter = false,
    };

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 10)]
    [InlineData(20, 10)]
    public void Delay_WithoutJitter_DoublesAndCaps(int attempt, int expectedSeconds) =>
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), Backoff.Delay(attempt, NoJitter));

    [Fact]
    public void Delay_WithJitter_StaysWithinZeroAndCap() {
        var options = NoJitter with { Jitter = true };
        var random = new Random(42);
        for (var attempt = 1; attempt <= 8; attempt++) {
            var delay = Backoff.Delay(attempt, options, random);
            Assert.InRange(delay, TimeSpan.Zero, Backoff.Cap(attempt, options));
        }
    }

    [Fact]
    public void Delay_WithJitter_IsNotConstant() {
        var options = NoJitter with { Jitter = true };
        var random = new Random(7);
        var samples = Enumerable.Range(0, 20).Select(_ => Backoff.Delay(4, options, random)).Distinct().Count();
        Assert.True(samples > 1);
    }

    [Fact]
    public void Delay_AttemptBelowOne_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Backoff.Delay(0, NoJitter));

    [Fact]
    public void Cap_HugeAttempt_DoesNotOverflow() =>
        Assert.Equal(TimeSpan.FromSeconds(10), Backoff.Cap(int.MaxValue, NoJitter));
}
