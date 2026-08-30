namespace EggIdentity.UI.Tests;

public class CalendarGridAnchorTests {
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static DateTimeOffset AtLocal(TimeZoneInfo tz, DateTime local) {
        return new DateTimeOffset(local, tz.GetUtcOffset(local));
    }

    private static DateOnly FindSpringForwardDate(TimeZoneInfo tz, int year) {
        var date = new DateOnly(year, 1, 1);
        var prevOffset = tz.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue));
        for (int i = 0; i < 365; i++) {
            date = date.AddDays(1);
            var offset = tz.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue));
            if (offset > prevOffset) {
                return date;
            }
            prevOffset = offset;
        }
        throw new InvalidOperationException("no spring-forward transition found in " + year);
    }

    [Fact]
    public void DayStart_UtcMidnightAnchor_BoundaryIsInclusiveOfSameDay() {
        var boundary = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        var atBoundary = CalendarGridAnchor.DayStart(boundary, TimeZoneInfo.Utc, TimeSpan.Zero);
        var oneSecondBefore = CalendarGridAnchor.DayStart(boundary.AddSeconds(-1), TimeZoneInfo.Utc, TimeSpan.Zero);
        var oneSecondAfter = CalendarGridAnchor.DayStart(boundary.AddSeconds(1), TimeZoneInfo.Utc, TimeSpan.Zero);

        Assert.Equal(boundary, atBoundary);
        Assert.Equal(boundary.AddDays(-1), oneSecondBefore);
        Assert.Equal(boundary, oneSecondAfter);
    }

    [Fact]
    public void DayStart_EasternNoonAnchor_BoundaryIsInclusiveOfSameDay() {
        var boundary = AtLocal(Eastern, new DateTime(2026, 1, 15, 12, 0, 0));

        var atBoundary = CalendarGridAnchor.DayStart(boundary, Eastern, TimeSpan.FromHours(12));
        var oneSecondBefore = CalendarGridAnchor.DayStart(boundary.AddSeconds(-1), Eastern, TimeSpan.FromHours(12));
        var oneSecondAfter = CalendarGridAnchor.DayStart(boundary.AddSeconds(1), Eastern, TimeSpan.FromHours(12));

        Assert.Equal(boundary, atBoundary);
        Assert.Equal(boundary.AddDays(-1), oneSecondBefore);
        Assert.Equal(boundary, oneSecondAfter);
    }

    [Fact]
    public void DayStart_AcrossSpringForwardTransition_NoDriftInOffsetOrWallClock() {
        var transitionDate = FindSpringForwardDate(Eastern, 2026);
        var anchor = TimeSpan.FromHours(12);

        var beforeDate = transitionDate.AddDays(-2);
        var afterDate = transitionDate;
        var beforeInstant = AtLocal(Eastern, beforeDate.ToDateTime(TimeOnly.FromTimeSpan(anchor)));
        var afterInstant = AtLocal(Eastern, afterDate.ToDateTime(TimeOnly.FromTimeSpan(anchor)));

        var dayStartBefore = CalendarGridAnchor.DayStart(beforeInstant, Eastern, anchor);
        var dayStartAfter = CalendarGridAnchor.DayStart(afterInstant, Eastern, anchor);

        Assert.Equal(beforeDate, DateOnly.FromDateTime(dayStartBefore.DateTime));
        Assert.Equal(afterDate, DateOnly.FromDateTime(dayStartAfter.DateTime));
        Assert.Equal(TimeOnly.FromTimeSpan(anchor), TimeOnly.FromDateTime(dayStartBefore.DateTime));
        Assert.Equal(TimeOnly.FromTimeSpan(anchor), TimeOnly.FromDateTime(dayStartAfter.DateTime));
        Assert.NotEqual(dayStartBefore.Offset, dayStartAfter.Offset);
    }

    [Fact]
    public void WeekStart_AcrossSpringForwardTransition_NoExceptionAndCorrectWeekday() {
        var transitionDate = FindSpringForwardDate(Eastern, 2026);
        var anchor = TimeSpan.FromHours(12);
        var instant = AtLocal(Eastern, transitionDate.ToDateTime(TimeOnly.FromTimeSpan(anchor)));

        var weekStart = CalendarGridAnchor.WeekStart(instant, Eastern, anchor);

        Assert.True(weekStart <= instant);
        Assert.Equal(DayOfWeek.Sunday, TimeZoneInfo.ConvertTime(weekStart, Eastern).DayOfWeek);
    }

    [Fact]
    public void WeekStart_DefaultSunday_ReturnsSundayOnOrBeforeInstant() {
        var instant = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        var weekStart = CalendarGridAnchor.WeekStart(instant, TimeZoneInfo.Utc, TimeSpan.Zero);

        Assert.Equal(DayOfWeek.Sunday, weekStart.DayOfWeek);
        Assert.True(weekStart <= instant);
    }

    [Fact]
    public void WeekStart_NonSundayStartDay_ReturnsRequestedWeekdayOnOrBeforeInstant() {
        var instant = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

        var weekStart = CalendarGridAnchor.WeekStart(instant, TimeZoneInfo.Utc, TimeSpan.Zero, DayOfWeek.Monday);

        Assert.Equal(DayOfWeek.Monday, weekStart.DayOfWeek);
        Assert.True(weekStart <= instant);
    }

    [Fact]
    public void DayStartForDate_UsesAnchorTimeOnGivenDate() {
        var date = new DateOnly(2026, 1, 15);
        var anchor = TimeSpan.FromHours(12);

        var result = CalendarGridAnchor.DayStartForDate(date, Eastern, anchor);

        Assert.Equal(date, DateOnly.FromDateTime(result.DateTime));
        Assert.Equal(TimeOnly.FromTimeSpan(anchor), TimeOnly.FromDateTime(result.DateTime));
        Assert.Equal(Eastern.GetUtcOffset(date.ToDateTime(TimeOnly.FromTimeSpan(anchor))), result.Offset);
    }

    [Fact]
    public void WeekStartForDate_DefaultSunday_MatchesWeekStartDate() {
        var date = new DateOnly(2026, 1, 15);

        var result = CalendarGridAnchor.WeekStartForDate(date, TimeZoneInfo.Utc, TimeSpan.Zero);
        var expectedDate = CalendarGridAnchor.WeekStartDate(date);

        Assert.Equal(expectedDate, DateOnly.FromDateTime(result.DateTime));
    }

    [Fact]
    public void WeekStartForDate_NonSundayStartDay_MatchesWeekStartDate() {
        var date = new DateOnly(2026, 1, 15);

        var result = CalendarGridAnchor.WeekStartForDate(date, TimeZoneInfo.Utc, TimeSpan.Zero, DayOfWeek.Monday);
        var expectedDate = CalendarGridAnchor.WeekStartDate(date, DayOfWeek.Monday);

        Assert.Equal(expectedDate, DateOnly.FromDateTime(result.DateTime));
    }

    [Fact]
    public void WeekStartForDate_AgreesWithWeekStartOfDayStartForDate() {
        var date = new DateOnly(2026, 1, 15);
        var anchor = TimeSpan.FromHours(12);

        var viaForDate = CalendarGridAnchor.WeekStartForDate(date, Eastern, anchor);
        var dayStart = CalendarGridAnchor.DayStartForDate(date, Eastern, anchor);
        var viaWeekStart = CalendarGridAnchor.WeekStart(dayStart, Eastern, anchor);

        Assert.Equal(viaForDate, viaWeekStart);
    }

    [Fact]
    public void WeekStartDate_NonSunday_ReturnsRequestedWeekdayOnOrBeforeDate() {
        var date = new DateOnly(2026, 1, 15);

        var result = CalendarGridAnchor.WeekStartDate(date, DayOfWeek.Monday);

        Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
        Assert.True(result <= date);
    }

    [Fact]
    public void DayStart_ZeroAnchorUtc_EqualsMidnightOfSameUtcDate() {
        var instant = new DateTimeOffset(2026, 6, 10, 15, 30, 0, TimeSpan.Zero);

        var result = CalendarGridAnchor.DayStart(instant, TimeZoneInfo.Utc, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void WeekStart_ZeroAnchorUtc_EqualsMidnightOfWeekStartDate() {
        var instant = new DateTimeOffset(2026, 6, 10, 15, 30, 0, TimeSpan.Zero);

        var result = CalendarGridAnchor.WeekStart(instant, TimeZoneInfo.Utc, TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, result.TimeOfDay);
        Assert.Equal(DayOfWeek.Sunday, result.DayOfWeek);
        Assert.True(result <= instant);
    }
}
