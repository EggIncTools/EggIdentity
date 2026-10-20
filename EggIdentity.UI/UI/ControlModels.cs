namespace EggIdentity.UI;

public sealed record SegmentedOption<T>(T Value, string Label, int? Count = null, string? Title = null, bool Disabled = false);

public sealed record ViewTab<TKey>(TKey Key, string Label, string? Sub = null, string? AccentVar = null);

public static class RatioStyle {
    public static double Clamp(double ratio) => double.IsNaN(ratio) ? 0 : Math.Clamp(ratio, 0, 1);

    public static string Var(double ratio) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"--ratio:{Clamp(ratio):0.###}");
}
