namespace EggIdentity.UI;

public sealed record CalendarLaneGroupSizing(int LaneCount, double LaneMinRem, double LaneGapRem, double HeaderRem);

public static class CalendarRowSizing {
    public static double RowHeightRem(IReadOnlyList<CalendarLaneGroupSizing> groups, double insetRem) {
        double total = insetRem;
        foreach (var g in groups) {
            var lanes = Math.Max(g.LaneCount, 0);
            total += g.HeaderRem;
            if (lanes > 0) {
                total += lanes * g.LaneMinRem + Math.Max(lanes - 1, 0) * g.LaneGapRem;
            }
        }
        return total;
    }
}
