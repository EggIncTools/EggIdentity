namespace EggIdentity.Diagrams.Tests;

public class LayeredLayoutTests {
    private static DiagramGraph Graph(IReadOnlyList<DiagramNode> nodes, IReadOnlyList<DiagramEdge> edges) => new(nodes, edges);

    private static PositionedNode At(DiagramLayout layout, string id) {
        var node = layout.Node(id);
        Assert.NotNull(node);
        return node;
    }

    [Theory]
    [InlineData(DiagramDirection.Down)]
    [InlineData(DiagramDirection.Right)]
    public void SingleParentChainsStayInTheirColumn(DiagramDirection direction) {
        var graph = Graph(
            [.. new[] { "R", "P1", "P2", "P3", "S1", "S2", "S3", "S4" }.Select(id => new DiagramNode(id) { Width = 160, Height = 88 })],
            [new DiagramEdge("R", "P1"), new DiagramEdge("P1", "P2"), new DiagramEdge("P2", "P3"),
                new DiagramEdge("R", "S1"), new DiagramEdge("S1", "S2"), new DiagramEdge("S2", "S3"), new DiagramEdge("S3", "S4")]);

        var layout = LayeredLayout.Layout(graph, ["R"], direction, DiagramStyle.Default);

        double Cross(string id) {
            return direction == DiagramDirection.Down ? At(layout, id).X : At(layout, id).Y;
        }
        Assert.Equal(Cross("P1"), Cross("P2"), 3);
        Assert.Equal(Cross("P2"), Cross("P3"), 3);
        Assert.Equal(Cross("S1"), Cross("S2"), 3);
        Assert.Equal(Cross("S2"), Cross("S3"), 3);
        Assert.Equal(Cross("S3"), Cross("S4"), 3);
        Assert.NotEqual(Cross("P1"), Cross("S1"));
    }

    [Fact]
    public void ChainRanksIncreaseByOne() {
        var graph = Graph(
            [new DiagramNode("A"), new DiagramNode("B"), new DiagramNode("C")],
            [new DiagramEdge("A", "B"), new DiagramEdge("B", "C")]);

        var layout = LayeredLayout.Layout(graph, ["A"], DiagramDirection.Down, DiagramStyle.Default);

        Assert.Equal(0, At(layout, "A").Rank);
        Assert.Equal(1, At(layout, "B").Rank);
        Assert.Equal(2, At(layout, "C").Rank);
    }

    [Fact]
    public void DiamondSharesOneChildNode() {
        var graph = Graph(
            [new DiagramNode("A"), new DiagramNode("B"), new DiagramNode("C"), new DiagramNode("D")],
            [new DiagramEdge("A", "B"), new DiagramEdge("A", "C"), new DiagramEdge("B", "D"), new DiagramEdge("C", "D")]);

        var layout = LayeredLayout.Layout(graph, ["A"], DiagramDirection.Down, DiagramStyle.Default);

        Assert.Single(layout.Nodes, n => n.Node.Id == "D");
        Assert.Equal(2, At(layout, "D").Rank);
    }

    [Fact]
    public void SkipEdgeGetsLongestPathRank() {
        var graph = Graph(
            [new DiagramNode("A"), new DiagramNode("B"), new DiagramNode("C")],
            [new DiagramEdge("A", "B"), new DiagramEdge("B", "C"), new DiagramEdge("A", "C")]);

        var layout = LayeredLayout.Layout(graph, ["A"], DiagramDirection.Down, DiagramStyle.Default);

        Assert.Equal(2, At(layout, "C").Rank);
    }

    [Fact]
    public void CycleFlagsExactlyOneBackEdgeAndTerminates() {
        var graph = Graph(
            [new DiagramNode("A"), new DiagramNode("B"), new DiagramNode("C")],
            [new DiagramEdge("A", "B"), new DiagramEdge("B", "C"), new DiagramEdge("C", "A")]);

        var layout = LayeredLayout.Layout(graph, ["A"], DiagramDirection.Down, DiagramStyle.Default);

        var backEdges = layout.Edges.Where(e => e.IsBack).ToList();
        Assert.Single(backEdges);
        Assert.Equal("C", backEdges[0].Edge.From);
        Assert.Equal("A", backEdges[0].Edge.To);
    }

    [Fact]
    public void CollapsedHidesDescendantsAndCountsHiddenChildren() {
        var graph = Graph(
            [new DiagramNode("A"), new DiagramNode("B"), new DiagramNode("C"), new DiagramNode("D")],
            [new DiagramEdge("A", "B"), new DiagramEdge("B", "C"), new DiagramEdge("C", "D")]);

        var layout = LayeredLayout.Layout(graph, ["A"], DiagramDirection.Down, DiagramStyle.Default, new HashSet<string> { "B" });

        Assert.Equal(2, layout.Nodes.Count);
        Assert.Null(layout.Node("C"));
        Assert.Null(layout.Node("D"));
        Assert.Equal(1, At(layout, "B").HiddenChildren);
    }

    [Fact]
    public void ReverseSwapsEdgeEndpointsAndKeepsPayload() {
        var graph = Graph(
            [new DiagramNode("A"), new DiagramNode("B")],
            [new DiagramEdge("A", "B") { Weight = 5, Label = "x" }]);

        var reversed = graph.Reverse();

        Assert.Equal("B", reversed.Edges[0].From);
        Assert.Equal("A", reversed.Edges[0].To);
        Assert.Equal(5, reversed.Edges[0].Weight);
        Assert.Equal("x", reversed.Edges[0].Label);
    }

    [Fact]
    public void RightDirectionSwapsAxesSoRanksIncreaseInX() {
        var graph = Graph(
            [new DiagramNode("A"), new DiagramNode("B"), new DiagramNode("C")],
            [new DiagramEdge("A", "B"), new DiagramEdge("B", "C")]);

        var layout = LayeredLayout.Layout(graph, ["A"], DiagramDirection.Right, DiagramStyle.Default);

        var a = At(layout, "A");
        var b = At(layout, "B");
        var c = At(layout, "C");
        Assert.True(a.X < b.X);
        Assert.True(b.X < c.X);
    }

    [Fact]
    public void WeightScalingHitsMinAndMaxEndpoints() {
        var graph = Graph(
            [new DiagramNode("A"), new DiagramNode("B"), new DiagramNode("C"), new DiagramNode("D")],
            [
                new DiagramEdge("A", "B") { Weight = 10 },
                new DiagramEdge("A", "C") { Weight = 0 },
                new DiagramEdge("A", "D"),
            ]);

        var layout = LayeredLayout.Layout(graph, ["A"], DiagramDirection.Down, DiagramStyle.Default);

        var style = DiagramStyle.Default;
        var toMax = layout.Edges.Single(e => e.Edge.To == "B");
        var toMin = layout.Edges.Single(e => e.Edge.To == "C");
        var toNull = layout.Edges.Single(e => e.Edge.To == "D");

        Assert.Equal(style.EdgeMaxWidth, toMax.Width);
        Assert.Equal(1.0, toMax.Opacity, 9);
        Assert.Equal(style.EdgeMinWidth, toMin.Width);
        Assert.Equal(style.EdgeMinOpacity, toMin.Opacity);
        Assert.Equal(style.EdgeMinWidth, toNull.Width);
        Assert.Equal(1.0, toNull.Opacity, 9);
    }

    [Fact]
    public void LayoutIsDeterministicAcrossRuns() {
        var graph = Graph(
            [new DiagramNode("A"), new DiagramNode("B"), new DiagramNode("C"), new DiagramNode("D"), new DiagramNode("E")],
            [
                new DiagramEdge("A", "B"),
                new DiagramEdge("A", "C"),
                new DiagramEdge("B", "D"),
                new DiagramEdge("C", "D") { Weight = 3 },
                new DiagramEdge("A", "E"),
            ]);

        var first = LayeredLayout.Layout(graph, ["A"], DiagramDirection.Down, DiagramStyle.Default);
        var second = LayeredLayout.Layout(graph, ["A"], DiagramDirection.Down, DiagramStyle.Default);

        foreach (var node in first.Nodes) {
            var other = At(second, node.Node.Id);
            Assert.Equal(node.X, other.X);
            Assert.Equal(node.Y, other.Y);
            Assert.Equal(node.Rank, other.Rank);
            Assert.Equal(node.Order, other.Order);
        }

        for (var i = 0; i < first.Edges.Count; i++) {
            Assert.Equal(first.Edges[i].Path, second.Edges[i].Path);
        }
    }

    [Fact]
    public void NoSameRankOverlapAfterCentering() {
        var graph = Graph(
            [
                new DiagramNode("Root"),
                new DiagramNode("Left"), new DiagramNode("Right"),
                new DiagramNode("L1"), new DiagramNode("L2"), new DiagramNode("L3"),
                new DiagramNode("R1"),
            ],
            [
                new DiagramEdge("Root", "Left"), new DiagramEdge("Root", "Right"),
                new DiagramEdge("Left", "L1"), new DiagramEdge("Left", "L2"), new DiagramEdge("Left", "L3"),
                new DiagramEdge("Right", "R1"),
            ]);

        var layout = LayeredLayout.Layout(graph, ["Root"], DiagramDirection.Down, DiagramStyle.Default);

        foreach (var rankGroup in layout.Nodes.GroupBy(n => n.Rank)) {
            var sorted = rankGroup.OrderBy(n => n.X).ToList();
            for (var i = 0; i < sorted.Count - 1; i++) {
                Assert.True(sorted[i].X + sorted[i].Width <= sorted[i + 1].X + 0.001);
            }
        }
    }

    [Fact]
    public void SplitRootOffByDefaultKeepsAllChildrenOnOneSide() {
        var graph = Graph(
            [.. new[] { "r", "c0", "c1", "c2", "c3" }.Select(id => new DiagramNode(id))],
            [new DiagramEdge("r", "c0"), new DiagramEdge("r", "c1"), new DiagramEdge("r", "c2"), new DiagramEdge("r", "c3")]);

        var layout = LayeredLayout.Layout(graph, ["r"], DiagramDirection.Down, DiagramStyle.Default);

        var root = At(layout, "r");
        foreach (var id in new[] { "c0", "c1", "c2", "c3" }) {
            var child = At(layout, id);
            Assert.Equal(1, child.Rank);
            Assert.True(child.Y > root.Y);
        }
    }

    [Fact]
    public void SplitRootDistributesTwelveChildrenAcrossBothSidesInDown() {
        var childIds = Enumerable.Range(0, 12).Select(i => $"c{i}").ToList();
        var graph = Graph(
            [new DiagramNode("r"), .. childIds.Select(id => new DiagramNode(id))],
            [.. childIds.Select(id => new DiagramEdge("r", id))]);

        var style = DiagramStyle.Default with { SplitRoot = true };
        var layout = LayeredLayout.Layout(graph, ["r"], DiagramDirection.Down, style);

        var root = At(layout, "r");
        var evenCount = 0;
        var oddCount = 0;
        for (var i = 0; i < childIds.Count; i++) {
            var child = At(layout, childIds[i]);
            if (i % 2 == 0) {
                Assert.Equal(1, child.Rank);
                Assert.True(child.Y > root.Y);
                Assert.Equal(DiagramDirection.Down, child.Direction);
                evenCount++;
            } else {
                Assert.Equal(-1, child.Rank);
                Assert.True(child.Y < root.Y);
                Assert.Equal(DiagramDirection.Up, child.Direction);
                oddCount++;
            }
        }
        Assert.Equal(6, evenCount);
        Assert.Equal(6, oddCount);
    }

    [Fact]
    public void SplitRootInRightPutsMirrorSideLeftOfRoot() {
        var graph = Graph(
            [.. new[] { "r", "c0", "c1", "c2", "c3" }.Select(id => new DiagramNode(id))],
            [new DiagramEdge("r", "c0"), new DiagramEdge("r", "c1"), new DiagramEdge("r", "c2"), new DiagramEdge("r", "c3")]);

        var style = DiagramStyle.Default with { SplitRoot = true };
        var layout = LayeredLayout.Layout(graph, ["r"], DiagramDirection.Right, style);

        var root = At(layout, "r");
        var c0 = At(layout, "c0");
        var c1 = At(layout, "c1");
        var c2 = At(layout, "c2");
        var c3 = At(layout, "c3");

        Assert.True(c0.X > root.X);
        Assert.True(c2.X > root.X);
        Assert.True(c1.X < root.X);
        Assert.True(c3.X < root.X);
        Assert.Equal(DiagramDirection.Left, c1.Direction);
        Assert.Equal(DiagramDirection.Left, c3.Direction);
    }

    [Fact]
    public void SplitRootCentersRootOverBothFans() {
        var graph = Graph(
            [.. new[] { "r", "c0", "c1", "c2", "c3" }.Select(id => new DiagramNode(id) { Width = 140, Height = 48 })],
            [new DiagramEdge("r", "c0"), new DiagramEdge("r", "c1"), new DiagramEdge("r", "c2"), new DiagramEdge("r", "c3")]);

        var style = DiagramStyle.Default with { SplitRoot = true };
        var layout = LayeredLayout.Layout(graph, ["r"], DiagramDirection.Down, style);

        double CenterX(string id) {
            var node = At(layout, id);
            return node.X + node.Width / 2;
        }

        var root = At(layout, "r");
        var rootCenter = root.X + root.Width / 2;
        var evenMid = (Math.Min(CenterX("c0"), CenterX("c2")) + Math.Max(CenterX("c0"), CenterX("c2"))) / 2;
        var oddMid = (Math.Min(CenterX("c1"), CenterX("c3")) + Math.Max(CenterX("c1"), CenterX("c3"))) / 2;

        Assert.Equal(evenMid, rootCenter, 2);
        Assert.Equal(oddMid, rootCenter, 2);
    }

    [Fact]
    public void SplitRootSkippedForSingleChildMultipleRootsOrCollapsedRoot() {
        var style = DiagramStyle.Default with { SplitRoot = true };

        var singleChildGraph = Graph(
            [new DiagramNode("r"), new DiagramNode("c0")],
            [new DiagramEdge("r", "c0")]);
        var singleChildLayout = LayeredLayout.Layout(singleChildGraph, ["r"], DiagramDirection.Down, style);
        Assert.All(singleChildLayout.Nodes, n => Assert.True(n.Rank >= 0));

        var multiRootGraph = Graph(
            [new DiagramNode("r1"), new DiagramNode("r2"), new DiagramNode("c0"), new DiagramNode("c1")],
            [new DiagramEdge("r1", "c0"), new DiagramEdge("r2", "c1")]);
        var multiRootLayout = LayeredLayout.Layout(multiRootGraph, ["r1", "r2"], DiagramDirection.Down, style);
        Assert.All(multiRootLayout.Nodes, n => Assert.True(n.Rank >= 0));

        var collapsedRootGraph = Graph(
            [new DiagramNode("r"), new DiagramNode("c0"), new DiagramNode("c1")],
            [new DiagramEdge("r", "c0"), new DiagramEdge("r", "c1")]);
        var collapsedRootLayout = LayeredLayout.Layout(collapsedRootGraph, ["r"], DiagramDirection.Down, style, new HashSet<string> { "r" });
        Assert.All(collapsedRootLayout.Nodes, n => Assert.True(n.Rank >= 0));
    }

    [Fact]
    public void SplitRootDeepSubtreeRanksGrowAwayFromRoot() {
        var graph = Graph(
            [new DiagramNode("r"), new DiagramNode("a"), new DiagramNode("b"), new DiagramNode("a1"), new DiagramNode("b1")],
            [new DiagramEdge("r", "a"), new DiagramEdge("r", "b"), new DiagramEdge("a", "a1"), new DiagramEdge("b", "b1")]);

        var style = DiagramStyle.Default with { SplitRoot = true };
        var layout = LayeredLayout.Layout(graph, ["r"], DiagramDirection.Down, style);

        var r = At(layout, "r");
        var b = At(layout, "b");
        var b1 = At(layout, "b1");

        Assert.Equal(2, At(layout, "a1").Rank);
        Assert.Equal(-2, b1.Rank);
        Assert.True(b1.Y < b.Y);
        Assert.True(b.Y < r.Y);
    }

    [Fact]
    public void SplitRootNodeReachableFromBothSidesIsOwnedOnceAndCrossEdgeIsBack() {
        var graph = Graph(
            [new DiagramNode("r"), new DiagramNode("a"), new DiagramNode("b"), new DiagramNode("s")],
            [new DiagramEdge("r", "a"), new DiagramEdge("r", "b"), new DiagramEdge("a", "s"), new DiagramEdge("b", "s")]);

        var style = DiagramStyle.Default with { SplitRoot = true };
        var layout = LayeredLayout.Layout(graph, ["r"], DiagramDirection.Down, style);

        Assert.Single(layout.Nodes, n => n.Node.Id == "s");
        Assert.Equal(2, At(layout, "s").Rank);

        var crossEdge = layout.Edges.Single(e => e.Edge.From == "b" && e.Edge.To == "s");
        Assert.True(crossEdge.IsBack);
    }

    [Fact]
    public void SplitRootLayoutStartsAtPadding() {
        var graph = Graph(
            [.. new[] { "r", "c0", "c1", "c2", "c3" }.Select(id => new DiagramNode(id))],
            [new DiagramEdge("r", "c0"), new DiagramEdge("r", "c1"), new DiagramEdge("r", "c2"), new DiagramEdge("r", "c3")]);

        var style = DiagramStyle.Default with { SplitRoot = true };
        var layout = LayeredLayout.Layout(graph, ["r"], DiagramDirection.Down, style);

        Assert.Equal(16, layout.Nodes.Min(n => n.X), 2);
        Assert.Equal(16, layout.Nodes.Min(n => n.Y), 2);
    }
}
