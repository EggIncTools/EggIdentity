namespace EggIdentity.Canvas.Tests;

public class BindingCatalogTests {
    private static readonly BindingCatalog Catalog = new([
        new BindableField("n", "Count", typeof(double), BindingRenderer.Number),
        new BindableField("d", "Time", typeof(TimeSpan), BindingRenderer.Duration),
        new BindableField("p", "Share", typeof(double), BindingRenderer.Percent),
        new BindableField("c", "Tint", typeof(string), BindingRenderer.Color),
        new BindableField("t", "Name", typeof(string), BindingRenderer.Text),
        new BindableField("u", "Upper", typeof(string), BindingRenderer.Text) { Format = v => v?.ToString()?.ToUpperInvariant() ?? "" },
    ]);

    [Theory]
    [InlineData("n", 60907.0, "60.9k")]
    [InlineData("p", 0.088, "8.8%")]
    [InlineData("c", "a1b2c3", "color:#a1b2c3")]
    [InlineData("t", "ship", "ship")]
    [InlineData("u", "ship", "SHIP")]
    public void FormatsByRendererOrCustom(string id, object value, string expected) => Assert.Equal(expected, Catalog.Format(id, value));

    [Fact]
    public void DurationUsesBrief() => Assert.Equal("2h 5m", Catalog.Format("d", TimeSpan.FromSeconds(7_500)));

    [Fact]
    public void NullRendersADash() => Assert.Equal("-", Catalog.Format("n", null));

    [Fact]
    public void UnknownIdNeverThrows() {
        Assert.Equal("42", Catalog.Format("missing", 42));
        Assert.Equal("-", Catalog.Format("missing", null));
        Assert.Null(Catalog.Find("missing"));
    }

    [Fact]
    public void FindReturnsTheField() => Assert.Equal(typeof(TimeSpan), Catalog.Find("d")!.ClrType);
}
