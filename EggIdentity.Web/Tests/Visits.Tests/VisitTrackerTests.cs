namespace EggIdentity.Visits.Tests;

public class VisitTrackerTests {
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static (VisitTracker tracker, FixedClock clock) Build() {
        var clock = new FixedClock(Start);
        return (new VisitTracker(new VisitsOptions("site"), clock), clock);
    }

    [Fact]
    public void CountsUniqueVisitorsAndPageviews() {
        var (tracker, _) = Build();
        tracker.Record("a", "/", 0);
        tracker.Record("a", "/x", 0);
        tracker.Record("b", "/", 0);

        var day = Assert.Single(tracker.Flush());
        Assert.Equal(2, day.Visitors);
        Assert.Equal(2, day.Visits);
        Assert.Equal(3, day.Pageviews);
        Assert.Equal(2, day.Paths["/"]);
        Assert.Equal(1, day.Paths["/x"]);
        Assert.False(day.Capped);
    }

    [Fact]
    public void DurationBeatsAddSecondsWithoutPageview() {
        var (tracker, _) = Build();
        tracker.Record("a", "/", 0);
        tracker.Record("a", "/", 42);

        var day = Assert.Single(tracker.Flush());
        Assert.Equal(1, day.Pageviews);
        Assert.Equal(42, day.DurationSeconds);
    }

    [Fact]
    public void SessionSplitsAfterGap() {
        var (tracker, clock) = Build();
        tracker.Record("a", "/", 0);
        clock.Advance(TimeSpan.FromMinutes(29));
        tracker.Record("a", "/", 0);
        clock.Advance(TimeSpan.FromMinutes(31));
        tracker.Record("a", "/", 0);

        var day = Assert.Single(tracker.Flush());
        Assert.Equal(1, day.Visitors);
        Assert.Equal(2, day.Visits);
    }

    [Fact]
    public void FlushReturnsDeltasAndKeepsVisitorSet() {
        var (tracker, _) = Build();
        tracker.Record("a", "/", 0);
        Assert.Single(tracker.Flush());
        Assert.Empty(tracker.Flush());

        tracker.Record("a", "/", 0);
        var day = Assert.Single(tracker.Flush());
        Assert.Equal(0, day.Visitors);
        Assert.Equal(0, day.Visits);
        Assert.Equal(1, day.Pageviews);
    }

    [Fact]
    public void RolloverClosesDayAndResetsVisitors() {
        var (tracker, clock) = Build();
        tracker.Record("a", "/", 0);
        clock.Advance(TimeSpan.FromDays(1));
        tracker.Record("a", "/", 0);

        var days = tracker.Flush();
        Assert.Equal(2, days.Count);
        Assert.All(days, d => Assert.Equal(1, d.Visitors));

        tracker.Record("a", "/", 0);
        var today = Assert.Single(tracker.Flush());
        Assert.Equal(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), today.Day);
        Assert.Equal(0, today.Visitors);
    }

    [Fact]
    public void VisitorCapDropsNewVisitorsAndFlags() {
        var (tracker, _) = Build();
        for (var i = 0; i <= VisitTracker.MaxVisitors; i++) tracker.Record(i.ToString(), "/", 0);

        var day = Assert.Single(tracker.Flush());
        Assert.Equal(VisitTracker.MaxVisitors, day.Visitors);
        Assert.Equal(VisitTracker.MaxVisitors, day.Pageviews);
        Assert.True(day.Capped);
    }

    [Fact]
    public void PathCapKeepsPageviewTotalAndFlags() {
        var (tracker, _) = Build();
        for (var i = 0; i <= VisitTracker.MaxPaths; i++) tracker.Record("a", "/p" + i, 0);

        var day = Assert.Single(tracker.Flush());
        Assert.Equal(VisitTracker.MaxPaths + 1, day.Pageviews);
        Assert.Equal(VisitTracker.MaxPaths, day.Paths.Count);
        Assert.True(day.Capped);
    }
}
