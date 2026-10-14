using System.Globalization;

namespace EggIdentity.Reports;

public sealed record SeriesColor(int Slot, string? Override);

public static class ChartPalette {
    public static (int R, int G, int B)? Rgb(string? hex) {
        if (!ReportCodec.IsHex(hex)) return null;
        var full = hex.Length == 4 ? new string(['#', hex[1], hex[1], hex[2], hex[2], hex[3], hex[3]]) : hex;
        return (Byte(full, 1), Byte(full, 3), Byte(full, 5));
    }

    public static string Hex(int r, int g, int b) =>
        string.Create(CultureInfo.InvariantCulture, $"#{Math.Clamp(r, 0, 255):x2}{Math.Clamp(g, 0, 255):x2}{Math.Clamp(b, 0, 255):x2}");

    public static (double H, double S, double L)? Hsl(string hex) {
        if (Rgb(hex) is not { } c) return null;
        var (r, g, b) = (c.R / 255.0, c.G / 255.0, c.B / 255.0);
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var d = max - min;
        if (d == 0) return (0d, 0d, l);
        var s = d / (1 - Math.Abs(2 * l - 1));
        var h = max == r ? Mod((g - b) / d + 6, 6) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60, s, l);
    }

    public static string FromHsl(double h, double s, double l) {
        var a = s * Math.Min(l, 1 - l);
        int F(int n) {
            var k = Mod(n + h / 30, 12);
            return (int)Math.Round(255 * (l - a * Math.Max(Math.Min(Math.Min(k - 3, 9 - k), 1), -1)));
        }
        return Hex(F(0), F(8), F(4));
    }

    public static IReadOnlyList<string> Slices(string baseColor, int count, double? lightness = null) {
        if (count <= 0 || Hsl(baseColor) is not { } hsl) return [];
        return [.. Enumerable.Range(0, count).Select(i => FromHsl(Mod(hsl.H + (double)i * 360 / count, 360), hsl.S, lightness ?? hsl.L))];
    }

    public static IReadOnlyDictionary<string, int> Slots(IEnumerable<string> keys, int slots) {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(slots);
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in keys)
            if (!map.ContainsKey(key)) map[key] = (int)(Hash(key) % (uint)slots) + 1;
        return map;
    }

    public static IReadOnlyList<SeriesColor> Resolve(IReadOnlyList<string> keys, ReportView view, ChartStyle style, double? lightness = null) {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(style);
        var slots = Slots(keys, style.SeriesSlots);
        var derived = (view.Color ?? style.SeriesBase) is { } c ? Slices(c, keys.Count, lightness) : [];
        return [.. keys.Select((k, i) => new SeriesColor(slots[k], Override(view, k) ?? (i < derived.Count ? derived[i] : null)))];
    }

    private static string? Override(ReportView view, string key) =>
        view.LabelColors.TryGetValue(key, out var own) && ReportCodec.IsHex(own) ? own : null;

    private static uint Hash(string key) {
        var h = 2166136261u;
        foreach (var ch in key) h = unchecked((h ^ ch) * 16777619u);
        return h;
    }

    private static int Byte(string hex, int start) =>
        int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static double Mod(double a, double n) {
        var r = a % n;
        return r < 0 ? r + n : r;
    }
}
