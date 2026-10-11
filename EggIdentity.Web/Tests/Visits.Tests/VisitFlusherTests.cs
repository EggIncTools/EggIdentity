namespace EggIdentity.Visits.Tests;

public class VisitFlusherTests {
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 23, 59, 30, TimeSpan.Zero);

    private sealed class CapturingSink : IVisitsSink {
        public List<(string Site, VisitsDaySnapshot Snapshot)> Writes { get; } = [];
        public bool Fail { get; set; }

        public Task UpsertDayAsync(string site, VisitsDaySnapshot snapshot, CancellationToken ct = default) {
            if (Fail) throw new InvalidOperationException("db down");
            Writes.Add((site, snapshot));
            return Task.CompletedTask;
        }

        public Task<VisitsDayState> ClaimDayAsync(string site, DateOnly day, byte[] salt, CancellationToken ct = default) {
            if (Fail) throw new InvalidOperationException("db down");
            return Task.FromResult(new VisitsDayState(salt, Seeded));
        }

        public Dictionary<string, long> Seeded { get; } = [with(StringComparer.Ordinal)];
    }

    private static (VisitTracker, CapturingSink, VisitFlusher) Build(FixedClock clock, bool fail = false) {
        var options = new VisitsOptions("site");
        var tracker = new VisitTracker(options, clock);
        var sink = new CapturingSink { Fail = fail };
        return (tracker, sink, new VisitFlusher(tracker, new VisitorHasher(clock), sink, options, clock));
    }

    [Fact]
    public async Task FlushWritesClosedAndOpenDaysThenClearsClosed() {
        var clock = new FixedClock(Start);
        var (tracker, sink, flusher) = Build(clock);

        tracker.Record("a", "/", 0);
        clock.Advance(TimeSpan.FromMinutes(1));
        tracker.Record("a", "/", 0);
        await flusher.FlushAsync(CancellationToken.None);

        Assert.Equal(2, sink.Writes.Count);
        Assert.All(sink.Writes, w => Assert.Equal("site", w.Site));
        Assert.Equal(new DateOnly(2026, 9, 18), sink.Writes[0].Snapshot.Day);
        Assert.Equal(new DateOnly(2026, 9, 19), sink.Writes[1].Snapshot.Day);

        tracker.Record("a", "/", 0);
        await flusher.FlushAsync(CancellationToken.None);
        Assert.Equal(3, sink.Writes.Count);
        Assert.Equal(0, sink.Writes[2].Snapshot.Visitors);
        Assert.Equal(1, sink.Writes[2].Snapshot.Pageviews);
    }

    [Fact]
    public async Task FailedFlushIsRetriedNextInterval() {
        var clock = new FixedClock(Start);
        var (tracker, sink, flusher) = Build(clock, fail: true);

        tracker.Record("a", "/", 0);
        await flusher.FlushAsync(CancellationToken.None);
        Assert.Empty(sink.Writes);

        sink.Fail = false;
        await flusher.FlushAsync(CancellationToken.None);
        var write = Assert.Single(sink.Writes);
        Assert.Equal(1, write.Snapshot.Visitors);
        Assert.Equal(1, write.Snapshot.Pageviews);
        Assert.Contains("a", write.Snapshot.Seen.Keys);
    }

    [Fact]
    public async Task ClaimedVisitorsAreNotRecountedAfterRestart() {
        var clock = new FixedClock(Start.AddHours(-12));
        var (tracker, sink, flusher) = Build(clock);
        sink.Seeded["a"] = clock.GetUtcNow().ToUnixTimeSeconds();

        await flusher.ClaimAsync(CancellationToken.None);
        tracker.Record("a", "/", 0);
        await flusher.FlushAsync(CancellationToken.None);

        var write = Assert.Single(sink.Writes);
        Assert.Equal(0, write.Snapshot.Visitors);
        Assert.Equal(0, write.Snapshot.Visits);
        Assert.Equal(1, write.Snapshot.Pageviews);
    }
}
