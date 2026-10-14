using System.Globalization;

namespace EggIdentity.Reports;

public abstract record DimensionOrder {
    private DimensionOrder() {
    }

    public static DimensionOrder Natural { get; } = new NaturalOrder();

    public static DimensionOrder OptionOrder { get; } = new DeclaredOrder();

    public static DimensionOrder Custom(Comparison<string> comparison) {
        ArgumentNullException.ThrowIfNull(comparison);
        return new CustomOrder(comparison);
    }

    public static int CompareNatural(string? a, string? b) {
        var x = a ?? "";
        var y = b ?? "";
        if (Number(x) is { } nx && Number(y) is { } ny) return nx.CompareTo(ny);
        return string.CompareOrdinal(x, y);
    }

    private static double? Number(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

    public sealed record NaturalOrder : DimensionOrder;

    public sealed record DeclaredOrder : DimensionOrder;

    public sealed record CustomOrder(Comparison<string> Comparison) : DimensionOrder;
}
