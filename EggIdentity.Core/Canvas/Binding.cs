using System.Globalization;
using EggIdentity.Formatting;

namespace EggIdentity.Canvas;

public enum BindingRenderer {
    Text,
    Number,
    Duration,
    Percent,
    Color,
    Sparkline,
    StackedBar,
    Gauge,
    Icon,
}

public sealed record BindableField(string Id, string Label, Type ClrType, BindingRenderer Renderer) {
    public Func<object?, string>? Format { get; init; }
}

public sealed class BindingCatalog(IEnumerable<BindableField> fields) {
    private readonly Dictionary<string, BindableField> _byId = (fields ?? throw new ArgumentNullException(nameof(fields))).ToDictionary(f => f.Id, StringComparer.Ordinal);

    public IReadOnlyList<BindableField> Fields { get; } = [.. fields];

    public BindableField? Find(string id) => _byId.GetValueOrDefault(id);

    public string Format(string id, object? value) {
        var field = Find(id);
        if (field?.Format is { } custom) return custom(value);
        if (value is null) return "-";
        return (field?.Renderer ?? BindingRenderer.Text) switch {
            BindingRenderer.Number when value is IConvertible => NumberFormat.Compact(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
            BindingRenderer.Percent when value is IConvertible => NumberFormat.Percent(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
            BindingRenderer.Duration when value is TimeSpan span => DurationFormat.Brief(span),
            BindingRenderer.Color when value is string hex => CssColor.HexStyle(hex) ?? "-",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "-",
        };
    }
}
