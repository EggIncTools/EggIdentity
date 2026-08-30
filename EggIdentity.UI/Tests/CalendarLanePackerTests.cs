namespace EggIdentity.UI.Tests;

public class CalendarLanePackerTests {
    [Fact]
    public void AssignLane_EmptyLaneList_AssignsLaneZeroAndAppends() {
        var laneRights = new List<double>();

        var lane = CalendarLanePacker.AssignLane(laneRights, 0.1, 0.3, 0.01);

        Assert.Equal(0, lane);
        Assert.Single(laneRights);
        Assert.Equal(0.3, laneRights[0]);
    }

    [Fact]
    public void AssignLane_FitsFreedLane_ReusesIndexAndUpdatesRight() {
        var laneRights = new List<double> { 0.2 };

        var lane = CalendarLanePacker.AssignLane(laneRights, 0.2, 0.5, 0.01);

        Assert.Equal(0, lane);
        Assert.Single(laneRights);
        Assert.Equal(0.5, laneRights[0]);
    }

    [Fact]
    public void AssignLane_DoesNotFitAnyLane_AppendsNewLane() {
        var laneRights = new List<double> { 0.5 };

        var lane = CalendarLanePacker.AssignLane(laneRights, 0.3, 0.7, 0.01);

        Assert.Equal(1, lane);
        Assert.Equal(2, laneRights.Count);
        Assert.Equal(0.5, laneRights[0]);
        Assert.Equal(0.7, laneRights[1]);
    }

    [Fact]
    public void AssignLane_LeftExactlyAtGapBoundary_FitsInclusive() {
        var laneRights = new List<double> { 0.5 };

        var lane = CalendarLanePacker.AssignLane(laneRights, 0.45, 0.9, 0.05);

        Assert.Equal(0, lane);
        Assert.Equal(0.9, laneRights[0]);
    }

    [Fact]
    public void AssignLane_LeftJustBelowGapBoundary_DoesNotFitAppendsNewLane() {
        var laneRights = new List<double> { 0.5 };

        var lane = CalendarLanePacker.AssignLane(laneRights, 0.449999, 0.9, 0.05);

        Assert.Equal(1, lane);
        Assert.Equal(2, laneRights.Count);
        Assert.Equal(0.5, laneRights[0]);
        Assert.Equal(0.9, laneRights[1]);
    }

    [Fact]
    public void AssignLane_SequentialIntervals_GreedyPacksAcrossLanesMatchingHandTrace() {
        var laneRights = new List<double>();
        var intervals = new (double Left, double Right)[] {
            (0.0, 0.3),
            (0.1, 0.4),
            (0.35, 0.5),
            (0.45, 0.6),
            (0.6, 0.8),
        };

        var assigned = new List<int>();
        foreach (var (left, right) in intervals) {
            assigned.Add(CalendarLanePacker.AssignLane(laneRights, left, right, 0.0));
        }

        Assert.Equal([0, 1, 0, 1, 0], assigned);
        Assert.Equal([0.8, 0.6], laneRights);
    }
}
