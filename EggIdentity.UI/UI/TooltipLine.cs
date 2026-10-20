namespace EggIdentity.UI;

public sealed record TooltipLine(string Text, bool Head = false, string? Class = null, string? Value = null, double? Ratio = null) {
    internal string Css {
        get {
            var css = Head ? "tooltip-line tooltip-line-head" : "tooltip-line";
            return string.IsNullOrEmpty(Class) ? css : $"{css} {Class}";
        }
    }

    internal string ValueCss => Ratio is null ? "tooltip-value" : "tooltip-value ratio-ink";

    internal string? ValueStyle => Ratio is { } r ? RatioStyle.Var(r) : null;
}
