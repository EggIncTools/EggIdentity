using System.Globalization;

namespace EggIdentity.Filters;

public static class RelativeDate {
    public static DateTimeOffset? Start(string value, DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(value);
        var text = value.Trim();
        if (text.Length < 2) return null;
        if (!int.TryParse(text.AsSpan(0, text.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return null;
        try {
            return text[^1] switch {
                'd' or 'D' => now.AddDays(-n),
                'w' or 'W' => now.AddDays(-7.0 * n),
                'm' or 'M' => now.AddMonths(-n),
                'y' or 'Y' => now.AddYears(-n),
                _ => null,
            };
        } catch (ArgumentOutOfRangeException) {
            return null;
        }
    }
}
