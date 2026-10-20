namespace EggIdentity.Canvas.Tests;

public class CanvasDocTests {
    private static CanvasItem Item(string payload) => new("a", "field", 1, 1, 2, 1, 0) { Payload = payload };

    [Fact]
    public void EqualItemsInOrderGiveEqualDocs() {
        var left = new CanvasDoc(4, 2, [Item("{\"field\":\"x\"}")]);
        var right = new CanvasDoc(4, 2, [Item("{\"field\":\"x\"}")]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, right with { Items = [Item("{\"field\":\"y\"}")] });
        Assert.NotEqual(left, right with { Cols = 5 });
    }

    [Fact]
    public void CssScalesByTheCanvasUnit() {
        var style = new ItemStyle { Border = "#fff", BorderWidth = 0.5, Background = "#000", Radius = 1, Padding = 0.25, Opacity = 0.5, Color = "#abc", FontSize = 1.5, FontWeight = 700, Align = TextAlign.Center };

        var css = style.Css();
        Assert.Contains("border:calc(0.5 * var(--canvas-unit)) solid #fff", css);
        Assert.Contains("border-radius:calc(1 * var(--canvas-unit))", css);
        Assert.Contains("padding:calc(0.25 * var(--canvas-unit))", css);
        Assert.Contains("font-size:calc(1.5 * var(--canvas-unit))", css);
        Assert.Contains("background-color:#000", css);
        Assert.Contains("color:#abc", css);
        Assert.Contains("opacity:0.5", css);
        Assert.Contains("font-weight:700", css);
        Assert.Contains("text-align:center", css);

        Assert.Contains("border:12px solid #fff", style.Css(24));
        Assert.Contains("font-size:36px", style.Css(24));
    }

    [Fact]
    public void EmptyStyleGivesNoCss() => Assert.Equal("", new ItemStyle().Css());
}
