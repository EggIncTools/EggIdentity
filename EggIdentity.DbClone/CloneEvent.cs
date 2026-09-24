namespace EggIdentity.DbClone;

public static class ClonePhase {
    public const string Guard = "guard";
    public const string Validate = "validate";
    public const string Order = "order";
    public const string Plan = "plan";
    public const string Truncate = "truncate";
    public const string Copy = "copy";
    public const string Scrub = "scrub";
    public const string Stamp = "stamp";
    public const string Done = "done";
    public const string Error = "error";
}

public sealed record CloneEvent(DateTimeOffset At, string Phase, string Message) {
    public string? Table { get; init; }
}

public enum CloneState {
    Idle,
    Running,
    Succeeded,
    Failed,
}

public sealed record CloneTableCount(string Table, ClonePolicy Policy, long SourceRows, long TargetRows);

public sealed record CloneStatus(
    CloneState State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Error,
    IReadOnlyList<CloneEvent> Events,
    IReadOnlyList<CloneTableCount> Tables);

public sealed class CloneTracker(int tail = 50, TimeProvider? time = null) : IProgress<CloneEvent> {
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly List<CloneEvent> _events = [];
    private IReadOnlyList<CloneTableCount> _tables = [];
    private CloneState _state = CloneState.Idle;
    private DateTimeOffset? _started;
    private DateTimeOffset? _finished;
    private string? _error;

    public void Report(CloneEvent value) {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate) {
            _events.Add(value);
            if (_events.Count > tail) _events.RemoveRange(0, _events.Count - tail);
        }
    }

    public void Start() {
        lock (_gate) {
            _state = CloneState.Running;
            _started = _time.GetUtcNow();
            _finished = null;
            _error = null;
            _events.Clear();
            _tables = [];
        }
    }

    public void Finish(IReadOnlyList<CloneTableCount> tables) {
        lock (_gate) {
            _state = CloneState.Succeeded;
            _finished = _time.GetUtcNow();
            _tables = tables;
        }
    }

    public void Fail(string error) {
        lock (_gate) {
            _state = CloneState.Failed;
            _finished = _time.GetUtcNow();
            _error = error;
        }
        Report(new CloneEvent(_time.GetUtcNow(), ClonePhase.Error, error));
    }

    public CloneStatus Snapshot() {
        lock (_gate) {
            return new CloneStatus(_state, _started, _finished, _error, [.. _events], _tables);
        }
    }
}
