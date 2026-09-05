namespace EggIdentity.Resilience.Tests;

public class CircuitBreakerTests {
    [Fact]
    public void Closed_AllowsEntry() {
        var breaker = new CircuitBreaker(3, TimeSpan.FromMinutes(1), new FakeTimeProvider());
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(breaker.TryEnter());
    }

    [Fact]
    public void FailuresBelowThreshold_StayClosed() {
        var breaker = new CircuitBreaker(3, TimeSpan.FromMinutes(1), new FakeTimeProvider());
        breaker.RecordFailure();
        breaker.RecordFailure();
        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(breaker.TryEnter());
    }

    [Fact]
    public void FailuresAtThreshold_Open_RejectsEntry() {
        var breaker = new CircuitBreaker(2, TimeSpan.FromMinutes(1), new FakeTimeProvider());
        breaker.RecordFailure();
        breaker.RecordFailure();
        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.False(breaker.TryEnter());
    }

    [Fact]
    public void Open_AfterDuration_AllowsSingleProbe() {
        var time = new FakeTimeProvider();
        var breaker = new CircuitBreaker(1, TimeSpan.FromMinutes(1), time);
        breaker.RecordFailure();
        Assert.False(breaker.TryEnter());

        time.Advance(TimeSpan.FromMinutes(1));

        Assert.True(breaker.TryEnter());
        Assert.Equal(CircuitState.HalfOpen, breaker.State);
        Assert.False(breaker.TryEnter());
    }

    [Fact]
    public void HalfOpen_Success_Closes() {
        var time = new FakeTimeProvider();
        var breaker = new CircuitBreaker(1, TimeSpan.FromMinutes(1), time);
        breaker.RecordFailure();
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(breaker.TryEnter());

        breaker.RecordSuccess();

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.True(breaker.TryEnter());
    }

    [Fact]
    public void HalfOpen_Failure_ReopensForFullDuration() {
        var time = new FakeTimeProvider();
        var breaker = new CircuitBreaker(5, TimeSpan.FromMinutes(1), time);
        for (var i = 0; i < 5; i++) breaker.RecordFailure();
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(breaker.TryEnter());

        breaker.RecordFailure();

        Assert.Equal(CircuitState.Open, breaker.State);
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.False(breaker.TryEnter());
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(breaker.TryEnter());
    }

    [Fact]
    public void Success_ResetsFailureCount() {
        var breaker = new CircuitBreaker(2, TimeSpan.FromMinutes(1), new FakeTimeProvider());
        breaker.RecordFailure();
        breaker.RecordSuccess();
        breaker.RecordFailure();
        Assert.Equal(CircuitState.Closed, breaker.State);
    }
}
