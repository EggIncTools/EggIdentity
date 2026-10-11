namespace EggIdentity.Visits;

public sealed class VisitTracker(VisitsOptions options, TimeProvider time) {
    public const int MaxVisitors = 200_000;
    public const int MaxPaths = 5_000;

    private sealed class DayState {
        public Dictionary<string, long> LastSeen { get; } = [with(StringComparer.Ordinal)];
        public HashSet<string> Dirty { get; } = [with(StringComparer.Ordinal)];
        public HashSet<string> PathsSeen { get; } = [with(StringComparer.Ordinal)];
        public Dictionary<string, long> PathViews { get; } = [with(StringComparer.Ordinal)];
        public long Visits { get; set; }
        public long Visitors { get; set; }
        public long Pageviews { get; set; }
        public long DurationSeconds { get; set; }
        public bool Capped { get; set; }
    }

    private readonly Lock _gate = new();
    private readonly Dictionary<DateOnly, DayState> _days = [];
    private readonly long _gapSeconds = (long)options.SessionGap.TotalSeconds;

    public void Record(string visitor, string path, int seconds) {
        ArgumentNullException.ThrowIfNull(visitor);
        ArgumentNullException.ThrowIfNull(path);
        var now = time.GetUtcNow();
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        var ts = now.ToUnixTimeSeconds();
        lock (_gate) {
            if (!_days.TryGetValue(day, out var state)) {
                state = new DayState();
                _days[day] = state;
            }

            if (state.LastSeen.TryGetValue(visitor, out var last)) {
                if (ts - last > _gapSeconds) state.Visits++;
            } else if (state.LastSeen.Count >= MaxVisitors) {
                state.Capped = true;
                return;
            } else {
                state.Visitors++;
                state.Visits++;
            }

            state.LastSeen[visitor] = ts;
            state.Dirty.Add(visitor);
            state.DurationSeconds += seconds;
            if (seconds > 0) return;

            state.Pageviews++;
            if (!state.PathsSeen.Contains(path)) {
                if (state.PathsSeen.Count >= MaxPaths) {
                    state.Capped = true;
                    return;
                }
                state.PathsSeen.Add(path);
            }
            state.PathViews[path] = state.PathViews.GetValueOrDefault(path) + 1;
        }
    }

    public void Seed(DateOnly day, IReadOnlyDictionary<string, long> lastSeen) {
        ArgumentNullException.ThrowIfNull(lastSeen);
        lock (_gate) {
            if (!_days.TryGetValue(day, out var state)) {
                state = new DayState();
                _days[day] = state;
            }
            foreach (var (visitor, ts) in lastSeen) {
                if (state.LastSeen.Count >= MaxVisitors) break;
                if (!state.LastSeen.TryGetValue(visitor, out var mine) || mine < ts) state.LastSeen[visitor] = ts;
            }
        }
    }

    public void Requeue(VisitsDaySnapshot snapshot) {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate) {
            if (!_days.TryGetValue(snapshot.Day, out var state)) {
                state = new DayState();
                _days[snapshot.Day] = state;
            }
            state.Visits += snapshot.Visits;
            state.Visitors += snapshot.Visitors;
            state.Pageviews += snapshot.Pageviews;
            state.DurationSeconds += snapshot.DurationSeconds;
            state.Capped |= snapshot.Capped;
            foreach (var (path, views) in snapshot.Paths) {
                state.PathsSeen.Add(path);
                state.PathViews[path] = state.PathViews.GetValueOrDefault(path) + views;
            }
            foreach (var (visitor, ts) in snapshot.Seen) {
                if (!state.LastSeen.TryGetValue(visitor, out var mine) || mine < ts) state.LastSeen[visitor] = ts;
                state.Dirty.Add(visitor);
            }
        }
    }

    public IReadOnlyList<VisitsDaySnapshot> Flush() {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        lock (_gate) {
            var result = new List<VisitsDaySnapshot>(_days.Count);
            foreach (var (day, state) in _days) {
                var snapshot = new VisitsDaySnapshot(
                    day, state.Visits, state.Visitors, state.Pageviews, state.DurationSeconds, state.Capped,
                    new Dictionary<string, long>(state.PathViews, StringComparer.Ordinal),
                    state.Dirty.ToDictionary(v => v, v => state.LastSeen[v], StringComparer.Ordinal));
                if (!snapshot.IsEmpty) result.Add(snapshot);
                state.Dirty.Clear();
                state.Visits = 0;
                state.Visitors = 0;
                state.Pageviews = 0;
                state.DurationSeconds = 0;
                state.PathViews.Clear();
            }

            foreach (var closed in _days.Keys.Where(d => d != today).ToList()) _days.Remove(closed);
            return result;
        }
    }
}
