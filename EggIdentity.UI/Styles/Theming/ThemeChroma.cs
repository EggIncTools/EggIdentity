using System.Text.Json.Serialization;

namespace EggIdentity.Styles.Theming;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ThemeHueRotate(
    [property: JsonPropertyName("enabled")] bool Enabled = false,
    [property: JsonPropertyName("seconds")] double Seconds = 30) {
    public ThemeHueRotate Clamped() => new(Enabled, Math.Clamp(Seconds, 6, 120));
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ThemeChroma(
    [property: JsonPropertyName("surfaceTint")] double SurfaceTint = 0,
    [property: JsonPropertyName("gradientHueShift")] double GradientHueShift = 0,
    [property: JsonPropertyName("glowRadius")] double GlowRadius = 0,
    [property: JsonPropertyName("glowAlpha")] double GlowAlpha = 0,
    [property: JsonPropertyName("hueRotate")] ThemeHueRotate? HueRotate = null) {
    public ThemeChroma Clamped() => new(
        Math.Clamp(SurfaceTint, 0, 12),
        Math.Clamp(GradientHueShift, -60, 60),
        Math.Clamp(GlowRadius, 0, 24),
        Math.Clamp(GlowAlpha, 0, 60),
        (HueRotate ?? new ThemeHueRotate()).Clamped());

    public static readonly ThemeChroma None = new(HueRotate: new ThemeHueRotate());
}
