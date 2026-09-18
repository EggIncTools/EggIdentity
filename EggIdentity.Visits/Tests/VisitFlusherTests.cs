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
    }

    [Fact]
    public async Task FlushWritesClosedAndOpenDaysThenClearsClosed() {
        var clock = new FixedClock(Start);
        var options = new VisitsOptions("site");
        var tracker = new VisitTracker(options, clock);
        var sink = new CapturingSink();
        var flusher = new VisitFlusher(tracker, sink, options, clock);

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
    public async Task FlushSwallowsSinkFailure() {
        var clock = new FixedClock(Start);
        var options = new VisitsOptions("site");
        var tracker = new VisitTracker(options, clock);
        var sink = new CapturingSink { Fail = true };
        var flusher = new VisitFlusher(tracker, sink, options, clock);

        tracker.Record("a", "/", 0);
        await flusher.FlushAsync(CancellationToken.None);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IdleFlushWritesNothing() {
        var clock = new FixedClock(Start);
        var options = new VisitsOptions("site");
        var sink = new CapturingSink();
        var flusher = new VisitFlusher(new VisitTracker(options, clock), sink, options, clock);

        await flusher.FlushAsync(CancellationToken.None);
        Assert.Empty(sink.Writes);
    }
}
