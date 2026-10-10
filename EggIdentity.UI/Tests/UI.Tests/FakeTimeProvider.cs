namespace EggIdentity.UI.Tests;

internal sealed class FakeTimeProvider : TimeProvider {
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _inner = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private int _active;

    public int ActiveTimers => _active;

    public void Advance(TimeSpan by) => _inner.Advance(by);

    public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        Interlocked.Increment(ref _active);
        return new Counted(this, _inner.CreateTimer(callback, state, dueTime, period));
    }

    private sealed class Counted(FakeTimeProvider owner, ITimer inner) : ITimer {
        private int _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);

        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) Interlocked.Decrement(ref owner._active);
            inner.Dispose();
        }

        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
