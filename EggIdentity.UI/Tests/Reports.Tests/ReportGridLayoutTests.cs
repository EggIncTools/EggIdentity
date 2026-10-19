using EggIdentity.UI.Reports;

namespace EggIdentity.Reports.Tests;

public class ReportGridLayoutTests {
    private static ReportPlacement Placement(string id, int order, int w = 1, int h = 1) =>
        new(id, 0, 0, w, h, order);

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(3, 4, 3, 4)]
    [InlineData(99, 99, 8, 8)]
    [InlineData(-2, -5, 1, 1)]
    public void ClampDims_ClampsToRange(int w, int h, int ew, int eh) {
        var (cw, ch) = ReportGridLayout.ClampDims(w, h);
        Assert.Equal(ew, cw);
        Assert.Equal(eh, ch);
    }

    [Fact]
    public void Pack_PacksRowMajorWrappingAtEightCols() {
        var placements = new[] { Placement("a", 0, 4, 2), Placement("b", 1, 4, 2), Placement("c", 2, 4, 2) };
        var packed = ReportGridLayout.Pack(placements);

        Assert.Equal((1, 1), (packed[0].X, packed[0].Y));
        Assert.Equal((5, 1), (packed[1].X, packed[1].Y));
        Assert.Equal((1, 3), (packed[2].X, packed[2].Y));
    }

    [Fact]
    public void Pack_FillsGapLeftByTallNeighbor() {
        var placements = new[] { Placement("a", 0, 2, 2), Placement("b", 1, 2, 1) };
        var packed = ReportGridLayout.Pack(placements);

        Assert.Equal((1, 1), (packed[0].X, packed[0].Y));
        Assert.Equal((3, 1), (packed[1].X, packed[1].Y));
    }

    [Fact]
    public void Pack_BackfillsHolesBehindEarlierCards() {
        var placements = new[] { Placement("a", 0, 6, 1), Placement("b", 1, 4, 1), Placement("c", 2, 2, 1) };
        var packed = ReportGridLayout.Pack(placements);

        Assert.Equal((1, 1), (packed[0].X, packed[0].Y));
        Assert.Equal((1, 2), (packed[1].X, packed[1].Y));
        Assert.Equal((7, 1), (packed[2].X, packed[2].Y));
    }

    [Fact]
    public void CellsFit_DetectsOverlap() {
        var occupied = new HashSet<(int, int)>();
        ReportGridLayout.MarkOccupied(occupied, 1, 1, 2, 2);
        Assert.False(ReportGridLayout.CellsFit(occupied, 2, 2, 2, 2));
        Assert.True(ReportGridLayout.CellsFit(occupied, 3, 1, 2, 2));
    }

    [Fact]
    public void FindPlacement_WrapsToNextRowWhenOverflowing() {
        var occupied = new HashSet<(int, int)>();
        var pos = ReportGridLayout.FindPlacement(occupied, 6, 1, 5, 1);
        Assert.Equal((1, 2), pos);
    }

    [Fact]
    public void Resize_UpdatesDimensionsAndRepacks() {
        var placements = new[] { Placement("a", 0, 2, 2), Placement("b", 1, 2, 2) };
        var resized = ReportGridLayout.Resize(placements, "a", 4, 1);

        var a = resized.Single(p => p.ReportId == "a");
        var b = resized.Single(p => p.ReportId == "b");
        Assert.Equal(4, a.W);
        Assert.Equal(1, a.H);
        Assert.Equal((1, 1), (a.X, a.Y));
        Assert.Equal((5, 1), (b.X, b.Y));
    }

    [Fact]
    public void MoveUp_SwapsOrderWithPrevious() {
        var placements = new[] { Placement("a", 0, 2, 2), Placement("b", 1, 2, 2) };
        var moved = ReportGridLayout.MoveUp(placements, "b");

        var a = moved.Single(p => p.ReportId == "a");
        var b = moved.Single(p => p.ReportId == "b");
        Assert.Equal((1, 1), (b.X, b.Y));
        Assert.Equal((3, 1), (a.X, a.Y));
    }

    [Fact]
    public void MoveUp_AtFrontIsNoOp() {
        var placements = new[] { Placement("a", 0, 2, 2), Placement("b", 1, 2, 2) };
        var moved = ReportGridLayout.MoveUp(placements, "a");

        var a = moved.Single(p => p.ReportId == "a");
        var b = moved.Single(p => p.ReportId == "b");
        Assert.Equal((1, 1), (a.X, a.Y));
        Assert.Equal((3, 1), (b.X, b.Y));
    }

    [Fact]
    public void MoveDown_AtEndIsNoOp() {
        var placements = new[] { Placement("a", 0, 2, 2), Placement("b", 1, 2, 2) };
        var moved = ReportGridLayout.MoveDown(placements, "b");

        var a = moved.Single(p => p.ReportId == "a");
        var b = moved.Single(p => p.ReportId == "b");
        Assert.Equal((1, 1), (a.X, a.Y));
        Assert.Equal((3, 1), (b.X, b.Y));
    }

    [Fact]
    public void ComputeEmptyZonesEquivalent_PackHandlesNoPlacements() =>
        Assert.Empty(ReportGridLayout.Pack([]));
}
