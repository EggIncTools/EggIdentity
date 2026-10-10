namespace EggIdentity.Resilience;

public enum CircuitState { Closed, Open, HalfOpen }

public sealed class CircuitBreaker(int failureThreshold, TimeSpan openDuration, TimeProvider? time = null) {
    private readonly TimeProvider _clock = time ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private int _failures;
    private DateTimeOffset _openedAt;
    private CircuitState _state = CircuitState.Closed;

    public CircuitState State {
        get {
            lock (_gate) {
                return _state;
            }
        }
    }

    public bool TryEnter() {
        lock (_gate) {
            switch (_state) {
                case CircuitState.Closed:
                    return true;
                case CircuitState.Open when _clock.GetUtcNow() - _openedAt >= openDuration:
                    _state = CircuitState.HalfOpen;
                    return true;
                default:
                    return false;
            }
        }
    }

    public void RecordSuccess() {
        lock (_gate) {
            _failures = 0;
            _state = CircuitState.Closed;
        }
    }

    public void RecordFailure() {
        lock (_gate) {
            _failures++;
            if (_state == CircuitState.HalfOpen || _failures >= failureThreshold) {
                _state = CircuitState.Open;
                _openedAt = _clock.GetUtcNow();
            }
        }
    }
}
