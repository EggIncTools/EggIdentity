using System.Globalization;

namespace EggIdentity.Reports;

#pragma warning disable CA1720
public enum ValueFormatKind {
    Integer,
    Decimal,
    Percent,
    Duration,
    Custom,
}

public sealed record ValueFormat(ValueFormatKind Kind, int Places = 0) {
    public static ValueFormat Integer { get; } = new(ValueFormatKind.Integer);

    public static ValueFormat Percent { get; } = new(ValueFormatKind.Percent, 1);

    public static ValueFormat Duration { get; } = new(ValueFormatKind.Duration);

    public static ValueFormat Decimal(int places) => new(ValueFormatKind.Decimal, Math.Clamp(places, 0, 10));

    public static ValueFormat Custom(Func<double, string> format) {
        ArgumentNullException.ThrowIfNull(format);
        return new ValueFormat(ValueFormatKind.Custom) { Formatter = format };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public Func<double, string>? Formatter { get; init; }

    public string Format(double value) => Kind switch {
        ValueFormatKind.Integer => Math.Round(value).ToString("0", CultureInfo.InvariantCulture),
        ValueFormatKind.Decimal => value.ToString("F" + Places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
        ValueFormatKind.Percent => value.ToString("F" + Places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%",
        ValueFormatKind.Duration => Span(value),
        _ => Formatter?.Invoke(value) ?? value.ToString(CultureInfo.InvariantCulture),
    };

    public string Axis(double value) => Kind switch {
        ValueFormatKind.Integer => Math.Round(value).ToString("0", CultureInfo.InvariantCulture),
        ValueFormatKind.Decimal => value.ToString(Places > 0 ? "0.0" : "0", CultureInfo.InvariantCulture),
        ValueFormatKind.Percent => value.ToString("0", CultureInfo.InvariantCulture) + "%",
        _ => Format(value),
    };

    private static string Span(double seconds) {
        var total = (long)Math.Round(Math.Abs(seconds));
        var sign = seconds < 0 ? "-" : "";
        var (d, h, m, s) = (total / 86400, total / 3600 % 24, total / 60 % 60, total % 60);
        if (d > 0) return string.Create(CultureInfo.InvariantCulture, $"{sign}{d}d {h}h");
        if (h > 0) return string.Create(CultureInfo.InvariantCulture, $"{sign}{h}h {m}m");
        if (m > 0) return string.Create(CultureInfo.InvariantCulture, $"{sign}{m}m {s}s");
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{s}s");
    }
}
#pragma warning restore CA1720
