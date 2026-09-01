using EggIdentity.Styles.Theming;

namespace EggIdentity.Styles.Tests;

public class ThemeChromaTests {
    [Fact]
    public void Clamped_ClampsOutOfRangeValues() {
        var chroma = new ThemeChroma(
            SurfaceTint: 999,
            GradientHueShift: 200,
            GlowRadius: 999,
            GlowAlpha: -50,
            HueRotate: new ThemeHueRotate());

        var clamped = chroma.Clamped();

        Assert.Equal(12, clamped.SurfaceTint);
        Assert.Equal(60, clamped.GradientHueShift);
        Assert.Equal(24, clamped.GlowRadius);
        Assert.Equal(0, clamped.GlowAlpha);
    }

    [Fact]
    public void Clamped_KeepsInRangeValuesUnchanged() {
        var chroma = new ThemeChroma(
            SurfaceTint: 6,
            GradientHueShift: -30,
            GlowRadius: 10,
            GlowAlpha: 40,
            HueRotate: new ThemeHueRotate());

        var clamped = chroma.Clamped();

        Assert.Equal(6, clamped.SurfaceTint);
        Assert.Equal(-30, clamped.GradientHueShift);
        Assert.Equal(10, clamped.GlowRadius);
        Assert.Equal(40, clamped.GlowAlpha);
    }

    [Fact]
    public void HueRotate_Clamped_ClampsSecondsToUpperBound() {
        Assert.Equal(120, new ThemeHueRotate(true, 999).Clamped().Seconds);
    }

    [Fact]
    public void HueRotate_Clamped_ClampsSecondsToLowerBound() {
        Assert.Equal(6, new ThemeHueRotate(true, 1).Clamped().Seconds);
    }
}
