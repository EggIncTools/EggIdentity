namespace EggIdentity.UI.Tests;

public class CalendarRowSizingTests {
    [Fact]
    public void RowHeightRem_SingleGroupWithLanes_AddsLaneAndGapWidth() {
        var groups = new[] { new CalendarLaneGroupSizing(3, 1.35, 0.15, 0) };

        var result = CalendarRowSizing.RowHeightRem(groups, 0.2);

        Assert.Equal(0.2 + 3 * 1.35 + 2 * 0.15, result, 3);
    }

    [Fact]
    public void RowHeightRem_TwoGroups_MatchesEggLedgerTwoGroupShape() {
        var groups = new[] {
            new CalendarLaneGroupSizing(0, 0, 0, 4.8),
            new CalendarLaneGroupSizing(2, 1.35, 0.15, 0),
        };

        var result = CalendarRowSizing.RowHeightRem(groups, 0.2);

        Assert.Equal(0.2 + 4.8 + 2 * 1.35 + 1 * 0.15 + CalendarRowSizing.GroupGapRem, result, 3);
    }

    [Fact]
    public void RowHeightRem_ThreeGroups_GeneralizesBeyondTwo() {
        var groups = new[] {
            new CalendarLaneGroupSizing(0, 0, 0, 1.2),
            new CalendarLaneGroupSizing(2, 1.0, 0.1, 0),
            new CalendarLaneGroupSizing(1, 0.8, 0.1, 0.4),
        };

        var result = CalendarRowSizing.RowHeightRem(groups, 0.3);

        Assert.Equal(0.3 + 1.2 + (2 * 1.0 + 1 * 0.1) + (0.4 + 1 * 0.8 + 0 * 0.1) + 2 * CalendarRowSizing.GroupGapRem, result, 3);
    }

    [Fact]
    public void RowHeightRem_EmptyGroupList_ReturnsInsetOnly() {
        var result = CalendarRowSizing.RowHeightRem([], 0.2);

        Assert.Equal(0.2, result, 3);
    }

    [Fact]
    public void RowHeightRem_NegativeLaneCount_TreatedAsZero() {
        var groups = new[] { new CalendarLaneGroupSizing(-1, 1.35, 0.15, 1.0) };

        var result = CalendarRowSizing.RowHeightRem(groups, 0.2);

        Assert.Equal(0.2 + 1.0, result, 3);
    }

    [Fact]
    public void RowHeightRem_CustomGroupGap_OverridesDefault() {
        var groups = new[] {
            new CalendarLaneGroupSizing(1, 1.0, 0, 0),
            new CalendarLaneGroupSizing(1, 1.0, 0, 0),
        };

        var result = CalendarRowSizing.RowHeightRem(groups, 0, groupGapRem: 0.25);

        Assert.Equal(2.25, result, 3);
    }
}
