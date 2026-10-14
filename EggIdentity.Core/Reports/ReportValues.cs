using System.Globalization;
using EggIdentity.Filters;

namespace EggIdentity.Reports;

internal static class ReportValues {
    public static IEnumerable<string> Texts(FilterSubjectValue value) => value switch {
        FilterSubjectValue.ManyValue m => m.Values,
        FilterSubjectValue.AbsentValue => [],
        _ => [Text(value)],
    };

    public static string? First(FilterSubjectValue value) => Texts(value).FirstOrDefault();

    public static IReadOnlyList<string> GroupKeys(FilterSubjectValue value, TimeBucket? bucket, TimeZoneInfo zone) => value switch {
        FilterSubjectValue.DateValue d => [TimeBuckets.Key(d.Value, bucket ?? TimeBucket.Day, zone)],
        FilterSubjectValue.ManyValue { Values.Count: > 0 } m => [.. m.Values.Distinct(StringComparer.Ordinal)],
        FilterSubjectValue.ManyValue or FilterSubjectValue.AbsentValue => [""],
        _ => [Text(value)],
    };

    private static string Text(FilterSubjectValue value) => value switch {
        FilterSubjectValue.TextValue t => t.Value,
        FilterSubjectValue.NumberValue n => n.Value.ToString(CultureInfo.InvariantCulture),
        FilterSubjectValue.BoolValue b => b.Value ? "true" : "false",
        FilterSubjectValue.DateValue d => d.Value.ToString("O", CultureInfo.InvariantCulture),
        FilterSubjectValue.ManyValue m => string.Join(',', m.Values),
        _ => "",
    };
}
