namespace EggIdentity.Resilience.Tests;

internal sealed class FakeTimeProvider : TimeProvider {
    private readonly Lock _gate = new();
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() {
        lock (_gate) {
            return _now;
        }
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => GetUtcNow().UtcTicks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
        var timer = new FakeTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by) {
        DateTimeOffset target;
        lock (_gate) {
            target = _now + by;
        }
        while (true) {
            FakeTimer? next;
            lock (_gate) {
                next = _timers.Where(t => t.DueAt is not null && t.DueAt <= target).MinBy(t => t.DueAt);
                if (next is null) {
                    _now = target;
                    break;
                }
                _now = next.DueAt!.Value;
                next.Reschedule(_now);
            }
            next.Fire();
        }
    }

    private void Register(FakeTimer timer) {
        lock (_gate) {
            if (!_timers.Contains(timer)) _timers.Add(timer);
        }
    }

    private void Unregister(FakeTimer timer) {
        lock (_gate) {
            _timers.Remove(timer);
        }
    }

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period) {
            _period = period;
            if (dueTime == Timeout.InfiniteTimeSpan) {
                DueAt = null;
                owner.Unregister(this);
                return true;
            }
            DueAt = owner.GetUtcNow() + dueTime;
            owner.Register(this);
            return true;
        }

        public void Reschedule(DateTimeOffset firedAt) {
            if (_period == Timeout.InfiniteTimeSpan || _period <= TimeSpan.Zero) {
                DueAt = null;
                owner._timers.Remove(this);
                return;
            }
            DueAt = firedAt + _period;
        }

        public void Fire() => callback(state);

        public void Dispose() {
            DueAt = null;
            owner.Unregister(this);
        }

        public ValueTask DisposeAsync() {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
