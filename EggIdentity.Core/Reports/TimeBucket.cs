using System.Globalization;

namespace EggIdentity.Reports;

public enum TimeBucket {
    Day,
    Week,
    Month,
    Year,
}

public static class TimeBuckets {
    public static IReadOnlyList<TimeBucket> All { get; } = [TimeBucket.Day, TimeBucket.Week, TimeBucket.Month, TimeBucket.Year];

    public static DateOnly Start(DateOnly day, TimeBucket bucket) => bucket switch {
        TimeBucket.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        TimeBucket.Month => new DateOnly(day.Year, day.Month, 1),
        TimeBucket.Year => new DateOnly(day.Year, 1, 1),
        _ => day,
    };

    public static DateOnly Day(DateTimeOffset value, TimeZoneInfo zone) {
        ArgumentNullException.ThrowIfNull(zone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, zone).DateTime);
    }

    public static string Key(DateTimeOffset value, TimeBucket bucket, TimeZoneInfo zone) => Key(Start(Day(value, zone), bucket), bucket);

    public static string Key(DateOnly start, TimeBucket bucket) => bucket switch {
        TimeBucket.Month => start.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        TimeBucket.Year => start.ToString("yyyy", CultureInfo.InvariantCulture),
        _ => start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    public static DateOnly? Parse(string key, TimeBucket bucket) {
        ArgumentNullException.ThrowIfNull(key);
        var format = bucket switch {
            TimeBucket.Month => "yyyy-MM",
            TimeBucket.Year => "yyyy",
            _ => "yyyy-MM-dd",
        };
        return DateOnly.TryParseExact(key, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? Start(d, bucket) : null;
    }

    public static DateOnly Next(DateOnly start, TimeBucket bucket) => bucket switch {
        TimeBucket.Week => start.AddDays(7),
        TimeBucket.Month => start.AddMonths(1),
        TimeBucket.Year => start.AddYears(1),
        _ => start.AddDays(1),
    };

    public static IReadOnlyList<string> Range(DateOnly first, DateOnly last, TimeBucket bucket) {
        var keys = new List<string>();
        for (var t = Start(first, bucket); t <= last; t = Next(t, bucket)) keys.Add(Key(t, bucket));
        return keys;
    }
}
