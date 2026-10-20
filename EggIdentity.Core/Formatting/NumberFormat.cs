using System.Globalization;

namespace EggIdentity.Formatting;

public static class NumberFormat {
    private static readonly string[] Suffixes = ["", "k", "M", "B", "T", "q", "Q", "s", "S", "o", "N", "d", "U", "D"];

    public static string Compact(double value, int digits = 3) {
        if (!double.IsFinite(value)) return "-";
        var magnitude = Math.Abs(value);
        if (magnitude < 1000) return Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
        digits = Math.Clamp(digits, 1, 6);
        var tier = (int)Math.Floor(Math.Log10(magnitude) / 3);
        var scaled = magnitude / Math.Pow(1000, tier);
        var rounded = RoundSignificant(scaled, digits);
        if (rounded >= 1000) {
            tier++;
            rounded = RoundSignificant(rounded / 1000, digits);
        }
        if (tier >= Suffixes.Length) return value.ToString("0.00e+0", CultureInfo.InvariantCulture);
        var decimals = Math.Max(0, digits - 1 - (int)Math.Floor(Math.Log10(rounded)));
        var text = rounded.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (text.Contains('.')) text = text.TrimEnd('0').TrimEnd('.');
        return (value < 0 ? "-" : "") + text + Suffixes[tier];
    }

    public static string Compact(long value, int digits = 3) => Compact((double)value, digits);

    public static string Full(double value) =>
        double.IsFinite(value) ? Math.Round(value).ToString("N0", CultureInfo.InvariantCulture) : "-";

    public static string Percent(double fraction, int digits = 2) {
        if (!double.IsFinite(fraction)) return "-";
        var magnitude = Math.Abs(fraction * 100);
        if (magnitude < double.Epsilon) return "0%";
        digits = Math.Clamp(digits, 1, 6);
        var rounded = RoundSignificant(magnitude, digits);
        if (rounded >= 100) return Full(fraction * 100) + "%";
        var decimals = Math.Max(0, digits - 1 - (int)Math.Floor(Math.Log10(rounded)));
        var text = rounded.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (text.Contains('.')) text = text.TrimEnd('0').TrimEnd('.');
        return (fraction < 0 ? "-" : "") + text + "%";
    }

    public static string Odds(double p) {
        if (!double.IsFinite(p) || p <= 0) return "-";
        return p < 0.001 ? "1 in " + Full(1 / p) : Percent(p);
    }

    private static double RoundSignificant(double value, int digits) {
        var scale = Math.Pow(10, digits - 1 - (int)Math.Floor(Math.Log10(value)));
        return Math.Round(value * scale) / scale;
    }
}

public static class DurationFormat {
    public static string Brief(TimeSpan span) {
        var sign = span < TimeSpan.Zero ? "-" : "";
        var total = (long)Math.Round(Math.Abs(span.TotalSeconds));
        var (d, h, m, s) = (total / 86400, total / 3600 % 24, total / 60 % 60, total % 60);
        return sign + (d, h, m) switch {
            ( > 0, _, _) => h > 0 ? $"{d}d {h}h" : $"{d}d",
            (_, > 0, _) => m > 0 ? $"{h}h {m}m" : $"{h}h",
            (_, _, > 0) => s > 0 ? $"{m}m {s}s" : $"{m}m",
            _ => $"{s}s",
        };
    }

    public static string Fractional(TimeSpan span) {
        var abs = span.Duration();
        var sign = span < TimeSpan.Zero ? "-" : "";
        var (value, unit) = abs.TotalDays >= 1 ? (abs.TotalDays, "d")
            : abs.TotalHours >= 1 ? (abs.TotalHours, "h")
            : abs.TotalMinutes >= 1 ? (abs.TotalMinutes, "m")
            : (abs.TotalSeconds, "s");
        var text = Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);
        return sign + text + unit;
    }

    public static string Countdown(DateTimeOffset target, TimeProvider clock) {
        ArgumentNullException.ThrowIfNull(clock);
        var remaining = target - clock.GetUtcNow();
        return remaining <= TimeSpan.Zero ? "now" : Brief(remaining);
    }
}

public static class ByteFormat {
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Humanize(long bytes) => Format(bytes, " ");

    public static string Compact(long bytes) => Format(bytes, "");

    private static string Format(long bytes, string gap) {
        var magnitude = Math.Abs((double)bytes);
        var tier = 0;
        while (magnitude >= 1024 && tier < Units.Length - 1) {
            magnitude /= 1024;
            tier++;
        }
        var sign = bytes < 0 ? "-" : "";
        var text = tier == 0
            ? magnitude.ToString("0", CultureInfo.InvariantCulture)
            : magnitude.ToString("0.0", CultureInfo.InvariantCulture);
        return sign + text + gap + Units[tier];
    }
}

public static class CssColor {
    public static string? HexStyle(string? sixHex, string property = "color") {
        if (sixHex is not { Length: 6 } || !sixHex.All(char.IsAsciiHexDigit)) return null;
        if (string.IsNullOrEmpty(property) || !property.All(c => c is '-' or (>= 'a' and <= 'z'))) return null;
        return $"{property}:#{sixHex.ToLowerInvariant()}";
    }
}
