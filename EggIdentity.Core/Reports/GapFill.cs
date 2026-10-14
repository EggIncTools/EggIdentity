using System.Globalization;
using EggIdentity.Filters;

namespace EggIdentity.Reports;

internal static class GapFill {
    private const int MaxBuckets = 5000;

    public static IReadOnlyList<string> Keys(
        IEnumerable<string> observed, ReportDimension dimension, TimeBucket bucket, FilterSet filters, TimeZoneInfo zone, DateTimeOffset now) {
        var days = observed.Select(k => TimeBuckets.Parse(k, bucket)).OfType<DateOnly>().ToList();
        var (lower, upper) = Range(dimension, filters, zone, now);
        var first = lower ?? (days.Count > 0 ? days.Min() : (DateOnly?)null);
        var last = upper ?? (days.Count > 0 ? days.Max() : (DateOnly?)null);
        if (first is not { } f || last is not { } l || f > l) return [];
        if (Span(f, l, bucket) <= MaxBuckets) return TimeBuckets.Range(f, l, bucket);
        return days.Count > 0 && Span(days.Min(), days.Max(), bucket) <= MaxBuckets ? TimeBuckets.Range(days.Min(), days.Max(), bucket) : [];
    }

    private static long Span(DateOnly first, DateOnly last, TimeBucket bucket) {
        var days = (long)last.DayNumber - first.DayNumber;
        return bucket switch {
            TimeBucket.Week => days / 7,
            TimeBucket.Month => days / 28,
            TimeBucket.Year => days / 365,
            _ => days,
        };
    }

    private static (DateOnly? Lower, DateOnly? Upper) Range(ReportDimension dimension, FilterSet filters, TimeZoneInfo zone, DateTimeOffset now) {
        DateOnly? lower = null, upper = null;
        for (var row = 0; row < filters.And.Count; row++) {
            if (row < filters.Or.Count && filters.Or[row].Count > 0) continue;
            var c = filters.And[row];
            if (!dimension.Field.Answers(c.Field)) continue;
            var (lo, hi) = Bounds(c, zone, now);
            if (lo is { } l) lower = Max(lower, l);
            if (hi is { } h) upper = Min(upper, h);
        }
        return (lower, upper);
    }

    private static (DateOnly? Lower, DateOnly? Upper) Bounds(FilterCondition c, TimeZoneInfo zone, DateTimeOffset now) {
        var op = FilterOps.Canonical(c.Op);
        if (op == FilterOps.Within)
            return RelativeDate.Start(c.Value, now) is { } start ? (TimeBuckets.Day(start, zone), TimeBuckets.Day(now, zone)) : (null, null);
        if (!DateTimeOffset.TryParse(c.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)) return (null, null);
        var day = TimeBuckets.Day(at, zone);
        return op switch {
            FilterOps.Gt or FilterOps.Gte => (day, null),
            FilterOps.Lt or FilterOps.Lte => (null, day),
            FilterOps.Eq or FilterOps.DayEq => (day, day),
            _ => (null, null),
        };
    }

    private static DateOnly Max(DateOnly? a, DateOnly b) => a is { } x && x > b ? x : b;

    private static DateOnly Min(DateOnly? a, DateOnly b) => a is { } x && x < b ? x : b;
}
