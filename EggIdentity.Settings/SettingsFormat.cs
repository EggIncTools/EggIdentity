using System.Globalization;

namespace EggIdentity.Settings;

public static class SettingsFormat {
    public static TimeSpan? ParseDuration(string? s) {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var total = TimeSpan.Zero;
        var num = "";
        foreach (var ch in s) {
            if (char.IsDigit(ch) || ch == '.') { num += ch; continue; }
            if (num.Length == 0) return null;
            if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
            switch (ch) {
                case 'h': total += TimeSpan.FromHours(value); break;
                case 'm': total += TimeSpan.FromMinutes(value); break;
                case 's': total += TimeSpan.FromSeconds(value); break;
                default: return null;
            }
            num = "";
        }
        return num.Length == 0 ? total : null;
    }

    public static IReadOnlyList<string> ParseList(string? s) {
        if (string.IsNullOrWhiteSpace(s)) return [];
        return [.. s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }
}
