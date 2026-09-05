namespace EggIdentity.UI.Tests;

internal sealed class FakeTimeProvider : TimeProvider {
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public int ActiveTimers => _timers.Count;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) {
        _now += by;
        foreach (var timer in _timers.ToArray()) timer.Fire(_now);
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        var timer = new FakeTimer(this, callback, state, _now + dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    private void Remove(FakeTimer timer) => _timers.Remove(timer);

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state, DateTimeOffset due, TimeSpan period) : ITimer {
        private DateTimeOffset _due = due;
        private TimeSpan _period = period;
        private bool _disposed;

        public void Fire(DateTimeOffset now) {
            while (!_disposed && _due <= now) {
                callback(state);
                if (_period <= TimeSpan.Zero) {
                    _due = DateTimeOffset.MaxValue;
                    return;
                }
                _due += _period;
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) {
            if (_disposed) return false;
            _due = owner._now + dueTime;
            _period = period;
            return true;
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            owner.Remove(this);
        }

        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
