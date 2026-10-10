namespace EggIdentity.Styles.Theming;

public static class ThemeDerivedTokens {
    public const string HueShift = "--theme-hue-shift";
    public const string Glow = "--theme-glow";
    public const string PanelTint = "--theme-panel-tint";
    public const string AccentGradTo = "--theme-accent-grad-to";

    public const string HueShiftPropertyRule =
        "@property --theme-hue-shift { syntax: '<angle>'; inherits: true; initial-value: 0deg; }";

    public static string GlowValue(double radiusPx, double alphaPercent) =>
        $"0 0 {radiusPx}px color-mix(in oklab, var(--color-accent) {alphaPercent}%, transparent),";

    public static string PanelTintValue(double tintPercent) =>
        $"color-mix(in oklab, var(--color-panel), var(--color-accent) {tintPercent}%)";
}
