namespace EggIdentity.Canvas.Tests;

public class CanvasOpsTests {
    private static CanvasDoc Doc(int snap = 1, params CanvasItem[] items) => new(8, 4, items) { Snap = snap };

    private static CanvasItem Item(string id, int x = 1, int y = 1, int w = 1, int h = 1, int z = 0) => new(id, "text", x, y, w, h, z);

    [Fact]
    public void AddSnapsClampsAndTakesTheTop() {
        var doc = CanvasOps.Add(Doc(2, Item("a")), Item("b", x: 2, y: 1, w: 3, h: 1));
        var added = doc.Items.Single(i => i.Id == "b");

        Assert.Equal((3, 1, 4, 2), (added.X, added.Y, added.W, added.H));
        Assert.Equal(1, added.Z);
    }

    [Fact]
    public void AddClampsIntoBounds() {
        var added = CanvasOps.Add(Doc(), Item("a", x: 7, y: 4, w: 3, h: 2)).Items.Single();

        Assert.Equal((6, 3, 3, 2), (added.X, added.Y, added.W, added.H));
    }

    [Fact]
    public void AddRejectsADuplicateId() =>
        Assert.Throws<ArgumentException>(() => CanvasOps.Add(Doc(1, Item("a")), Item("a")));

    [Fact]
    public void RemoveRenumbersZ() {
        var doc = CanvasOps.Remove(Doc(1, Item("a"), Item("b", z: 1), Item("c", z: 2)), "b");

        Assert.Equal([("a", 0), ("c", 1)], doc.Items.Select(i => (i.Id, i.Z)));
    }

    [Fact]
    public void MoveClampsToBounds() {
        var moved = CanvasOps.Move(Doc(1, Item("a", w: 3)), "a", 10, 0).Items.Single();

        Assert.Equal((6, 1), (moved.X, moved.Y));
    }

    [Fact]
    public void ResizeKeepsOneCellAndTheEdge() {
        var doc = Doc(1, Item("a", x: 3));

        Assert.Equal((1, 1), (CanvasOps.Resize(doc, "a", 0, 0).Items[0].W, CanvasOps.Resize(doc, "a", 0, 0).Items[0].H));
        Assert.Equal((6, 4), (CanvasOps.Resize(doc, "a", 20, 20).Items[0].W, CanvasOps.Resize(doc, "a", 20, 20).Items[0].H));
    }

    [Fact]
    public void ForwardAndBackSwapZWithTheNeighbour() {
        var doc = Doc(1, Item("a"), Item("b", z: 1), Item("c", z: 2));

        Assert.Equal(1, CanvasOps.BringForward(doc, "a").Items.Single(i => i.Id == "a").Z);
        Assert.Equal(0, CanvasOps.BringForward(doc, "a").Items.Single(i => i.Id == "b").Z);
        Assert.Equal(2, CanvasOps.BringToFront(doc, "a").Items.Single(i => i.Id == "a").Z);
        Assert.Equal(0, CanvasOps.SendToBack(doc, "c").Items.Single(i => i.Id == "c").Z);
        Assert.Equal(2, CanvasOps.BringForward(doc, "c").Items.Single(i => i.Id == "c").Z);
    }

    [Fact]
    public void DuplicateOffsetsBySnapAndTakesTheTop() {
        var doc = CanvasOps.Duplicate(Doc(2, Item("a", x: 1, y: 1, w: 2, h: 2), Item("b", z: 1)), "a", "a2");
        var copy = doc.Items.Single(i => i.Id == "a2");

        Assert.Equal((3, 3, 2), (copy.X, copy.Y, copy.Z));
    }

    [Fact]
    public void NudgeMovesByCells() {
        var nudged = CanvasOps.Nudge(Doc(1, Item("a", x: 2, y: 2)), "a", 1, -1).Items.Single();

        Assert.Equal((3, 1), (nudged.X, nudged.Y));
    }

    [Fact]
    public void CleanDropsBadColoursAndWeight() {
        var style = new ItemStyle { Border = "red", Background = "#abc", Color = "#12345", FontWeight = 950, Opacity = 1.5 };
        var cleaned = CanvasOps.Clean(Doc(1, Item("a") with { Style = style })).Items.Single().Style!;

        Assert.Null(cleaned.Border);
        Assert.Equal("#abc", cleaned.Background);
        Assert.Null(cleaned.Color);
        Assert.Null(cleaned.FontWeight);
        Assert.Null(cleaned.Opacity);
    }

    [Fact]
    public void UnknownIdReturnsTheSameDoc() {
        var doc = Doc(1, Item("a"));

        Assert.Same(doc, CanvasOps.Move(doc, "zz", 2, 2));
        Assert.Same(doc, CanvasOps.Remove(doc, "zz"));
    }
}
