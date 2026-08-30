using Microsoft.AspNetCore.Components;

namespace EggIdentity.UI;

public sealed record CalendarCellLabel(double PositionPercent, string Text, bool Muted = false);

public sealed record CalendarLaneGroupRow<TItem>(IReadOnlyList<IReadOnlyList<TItem>> Lanes);

public sealed record CalendarRow<TItem>(
    IReadOnlyList<CalendarCellLabel> CellLabels,
    IReadOnlyList<double> GridLinePositions,
    IReadOnlyList<double> HourTickPositions,
    double? NowLinePosition,
    IReadOnlyList<CalendarLaneGroupRow<TItem>> LaneGroups);

public sealed record PeriodSlot<TItem>(IReadOnlyList<CalendarRow<TItem>> Rows);

public sealed record LaneGroupDef<TItem>(RenderFragment<TItem> ItemTemplate, double LaneMinRem, double LaneGapRem, double HeaderRem);
