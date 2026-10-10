namespace EggIdentity.UI;

public sealed record TooltipLine(string Text, bool Head = false, string? Class = null) {
    internal string Css {
        get {
            var css = Head ? "tooltip-line tooltip-line-head" : "tooltip-line";
            return string.IsNullOrEmpty(Class) ? css : $"{css} {Class}";
        }
    }
}
