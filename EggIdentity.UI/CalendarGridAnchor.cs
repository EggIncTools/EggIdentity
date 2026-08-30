namespace EggIdentity.UI;

public static class CalendarGridAnchor {
    public static DateTimeOffset DayStart(DateTimeOffset instant, TimeZoneInfo tz, TimeSpan anchor) {
        var local = TimeZoneInfo.ConvertTime(instant, tz);
        var boundaryDate = local.TimeOfDay < anchor ? local.Date.AddDays(-1) : local.Date;
        var anchorLocal = boundaryDate.Add(anchor);
        return new DateTimeOffset(anchorLocal, tz.GetUtcOffset(anchorLocal));
    }

    public static DateTimeOffset WeekStart(DateTimeOffset instant, TimeZoneInfo tz, TimeSpan anchor, DayOfWeek weekStartDay = DayOfWeek.Sunday) {
        var gridDay = DayStart(instant, tz, anchor);
        var localDayOfWeek = TimeZoneInfo.ConvertTime(gridDay, tz).DayOfWeek;
        int offset = ((int)localDayOfWeek - (int)weekStartDay + 7) % 7;
        return gridDay.AddDays(-offset);
    }

    public static DateTimeOffset DayStartForDate(DateOnly date, TimeZoneInfo tz, TimeSpan anchor) {
        var anchorLocal = date.ToDateTime(TimeOnly.FromTimeSpan(anchor));
        return new DateTimeOffset(anchorLocal, tz.GetUtcOffset(anchorLocal));
    }

    public static DateTimeOffset WeekStartForDate(DateOnly date, TimeZoneInfo tz, TimeSpan anchor, DayOfWeek weekStartDay = DayOfWeek.Sunday) {
        var gridDay = DayStartForDate(date, tz, anchor);
        int offset = ((int)date.DayOfWeek - (int)weekStartDay + 7) % 7;
        return gridDay.AddDays(-offset);
    }

    public static DateOnly WeekStartDate(DateOnly date, DayOfWeek weekStartDay = DayOfWeek.Sunday) {
        int offset = ((int)date.DayOfWeek - (int)weekStartDay + 7) % 7;
        return date.AddDays(-offset);
    }
}
