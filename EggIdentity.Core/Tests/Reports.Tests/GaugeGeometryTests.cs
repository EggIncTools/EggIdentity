namespace EggIdentity.Reports.Tests;

public class GaugeGeometryTests {
    [Theory]
    [InlineData(5, 2, 1)]
    [InlineData(-1, 2, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(1, 4, 0.25)]
    public void FractionIsClamped(double value, double max, double expected) =>
        Assert.Equal(expected, GaugeGeometry.Layout(value, max, 48).Fraction);

    [Fact]
    public void TrackIsAnArcAndZeroFillIsEmpty() {
        var layout = GaugeGeometry.Layout(0, 10, 48);

        Assert.StartsWith("M", layout.TrackPath, StringComparison.Ordinal);
        Assert.Contains(" A ", layout.TrackPath, StringComparison.Ordinal);
        Assert.Equal("", layout.FillPath);
        Assert.Equal((24, 24), (layout.Cx, layout.Cy));
    }

    [Fact]
    public void HalfFillStartsWhereTheTrackStarts() {
        var layout = GaugeGeometry.Layout(5, 10, 48);

        Assert.Equal(layout.TrackPath[..layout.TrackPath.IndexOf(" A ", StringComparison.Ordinal)], layout.FillPath[..layout.FillPath.IndexOf(" A ", StringComparison.Ordinal)]);
        Assert.NotEqual(layout.TrackPath, layout.FillPath);
    }
}
