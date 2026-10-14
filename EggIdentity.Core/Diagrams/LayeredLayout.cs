using System.Globalization;

namespace EggIdentity.Diagrams;

public static class LayeredLayout {
    public static DiagramLayout Layout(DiagramGraph graph, IReadOnlyList<string> roots, DiagramDirection direction, DiagramStyle style, IReadOnlySet<string>? collapsed = null) {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(style);

        var collapsedSet = collapsed ?? (IReadOnlySet<string>)new HashSet<string>();
        var nodesById = graph.Nodes.ToDictionary(n => n.Id);
        var known = new HashSet<string>(nodesById.Keys);
        var knownEdges = graph.Edges.Where(e => known.Contains(e.From) && known.Contains(e.To)).ToList();
        var edgesByFrom = knownEdges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => (IReadOnlyList<DiagramEdge>)[.. g]);

        var (visibleOrder, visibleSet, rootSet) = ComputeVisible(roots, known, edgesByFrom, collapsedSet);
        var hiddenChildren = ComputeHiddenChildren(graph, edgesByFrom, visibleSet, collapsedSet);
        var backEdges = ComputeBackEdges(roots, visibleSet, edgesByFrom);
        var forwardEdges = knownEdges.Where(e => visibleSet.Contains(e.From) && visibleSet.Contains(e.To) && !backEdges.Contains(e)).ToList();
        var forwardByFrom = forwardEdges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => (IReadOnlyList<DiagramEdge>)[.. g]);

        var rank = ComputeRanks(visibleOrder, forwardEdges, rootSet);
        var order = ComputeOrder(visibleOrder, rank, forwardEdges, style.Sweeps);
        var positioned = ComputePositions(nodesById, visibleOrder, rank, order, forwardByFrom, direction, style, rootSet, hiddenChildren);
        var positionedEdges = ComputeEdges(knownEdges, visibleSet, backEdges, positioned, direction, style);

        var width = positioned.Count == 0 ? 0 : positioned.Max(n => n.X + n.Width) + style.Padding;
        var height = positioned.Count == 0 ? 0 : positioned.Max(n => n.Y + n.Height) + style.Padding;

        return new DiagramLayout(width, height, positioned, positionedEdges);
    }

    private static (List<string> Order, HashSet<string> Set, HashSet<string> Roots) ComputeVisible(IReadOnlyList<string> roots, HashSet<string> known, Dictionary<string, IReadOnlyList<DiagramEdge>> edgesByFrom, IReadOnlySet<string> collapsed) {
        var visible = new HashSet<string>();
        var order = new List<string>();
        var rootSet = new HashSet<string>();
        var queue = new Queue<string>();

        foreach (var id in roots) {
            if (!known.Contains(id)) {
                continue;
            }
            rootSet.Add(id);
            if (visible.Add(id)) {
                order.Add(id);
                queue.Enqueue(id);
            }
        }

        while (queue.Count > 0) {
            var id = queue.Dequeue();
            if (collapsed.Contains(id)) {
                continue;
            }
            if (!edgesByFrom.TryGetValue(id, out var outgoing)) {
                continue;
            }
            foreach (var edge in outgoing) {
                if (visible.Add(edge.To)) {
                    order.Add(edge.To);
                    queue.Enqueue(edge.To);
                }
            }
        }

        return (order, visible, rootSet);
    }

    private static Dictionary<string, int> ComputeHiddenChildren(DiagramGraph graph, Dictionary<string, IReadOnlyList<DiagramEdge>> edgesByFrom, HashSet<string> visible, IReadOnlySet<string> collapsed) {
        var hidden = new Dictionary<string, int>();
        foreach (var node in graph.Nodes) {
            if (!collapsed.Contains(node.Id) || !visible.Contains(node.Id)) {
                continue;
            }
            if (!edgesByFrom.TryGetValue(node.Id, out var outgoing)) {
                hidden[node.Id] = 0;
                continue;
            }
            var childIds = outgoing.Select(e => e.To).Distinct();
            hidden[node.Id] = childIds.Count(id => !visible.Contains(id));
        }
        return hidden;
    }

    private static HashSet<DiagramEdge> ComputeBackEdges(IReadOnlyList<string> roots, HashSet<string> visible, Dictionary<string, IReadOnlyList<DiagramEdge>> edgesByFrom) {
        var backEdges = new HashSet<DiagramEdge>();
        var visited = new HashSet<string>();
        var onStack = new HashSet<string>();
        var stack = new Stack<(string Id, int Index)>();

        foreach (var rootId in roots) {
            if (!visible.Contains(rootId) || !visited.Add(rootId)) {
                continue;
            }
            onStack.Add(rootId);
            stack.Push((rootId, 0));

            while (stack.Count > 0) {
                var (id, index) = stack.Pop();
                var outgoing = edgesByFrom.TryGetValue(id, out var list) ? list : [];
                var descended = false;
                for (var i = index; i < outgoing.Count; i++) {
                    var edge = outgoing[i];
                    if (!visible.Contains(edge.To)) {
                        continue;
                    }
                    if (onStack.Contains(edge.To)) {
                        backEdges.Add(edge);
                        continue;
                    }
                    if (visited.Add(edge.To)) {
                        stack.Push((id, i + 1));
                        stack.Push((edge.To, 0));
                        onStack.Add(edge.To);
                        descended = true;
                        break;
                    }
                }
                if (!descended) {
                    onStack.Remove(id);
                }
            }
        }

        return backEdges;
    }

    private static Dictionary<string, int> ComputeRanks(List<string> visibleOrder, List<DiagramEdge> forwardEdges, HashSet<string> rootSet) {
        var predecessors = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();
        foreach (var id in visibleOrder) {
            inDegree[id] = 0;
        }
        foreach (var edge in forwardEdges) {
            inDegree[edge.To]++;
            if (!predecessors.TryGetValue(edge.To, out var list)) {
                list = [];
                predecessors[edge.To] = list;
            }
            list.Add(edge.From);
        }

        var forwardByFrom = forwardEdges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => (IReadOnlyList<DiagramEdge>)[.. g]);
        var remaining = new Dictionary<string, int>(inDegree);
        var queue = new Queue<string>();
        foreach (var id in visibleOrder) {
            if (inDegree[id] == 0) {
                queue.Enqueue(id);
            }
        }

        var topo = new List<string>();
        while (queue.Count > 0) {
            var id = queue.Dequeue();
            topo.Add(id);
            if (!forwardByFrom.TryGetValue(id, out var outgoing)) {
                continue;
            }
            foreach (var edge in outgoing) {
                remaining[edge.To]--;
                if (remaining[edge.To] == 0) {
                    queue.Enqueue(edge.To);
                }
            }
        }

        var rank = new Dictionary<string, int>();
        foreach (var id in topo) {
            var best = 0;
            if (predecessors.TryGetValue(id, out var preds)) {
                foreach (var pred in preds) {
                    if (rank.TryGetValue(pred, out var predRank) && predRank + 1 > best) {
                        best = predRank + 1;
                    }
                }
            }
            rank[id] = best;
        }

        foreach (var id in rootSet) {
            if (rank.ContainsKey(id)) {
                rank[id] = 0;
            }
        }

        return rank;
    }

    private static Dictionary<string, int> ComputeOrder(List<string> visibleOrder, Dictionary<string, int> rank, List<DiagramEdge> forwardEdges, int sweeps) {
        var rankBuckets = new Dictionary<int, List<string>>();
        foreach (var id in visibleOrder) {
            var r = rank[id];
            if (!rankBuckets.TryGetValue(r, out var bucket)) {
                bucket = [];
                rankBuckets[r] = bucket;
            }
            bucket.Add(id);
        }

        var orderIndex = new Dictionary<string, int>();
        foreach (var bucket in rankBuckets.Values) {
            for (var i = 0; i < bucket.Count; i++) {
                orderIndex[bucket[i]] = i;
            }
        }

        if (rankBuckets.Count == 0) {
            return orderIndex;
        }

        var minRank = rankBuckets.Keys.Min();
        var maxRank = rankBuckets.Keys.Max();
        var parentsOf = forwardEdges.GroupBy(e => e.To).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(e => e.From)]);
        var childrenOf = forwardEdges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)[.. g.Select(e => e.To)]);

        for (var pass = 0; pass < sweeps; pass++) {
            var down = pass % 2 == 0;
            if (down) {
                for (var r = minRank + 1; r <= maxRank; r++) {
                    ReorderRank(rankBuckets, orderIndex, rank, r, r - 1, parentsOf);
                }
            } else {
                for (var r = maxRank - 1; r >= minRank; r--) {
                    ReorderRank(rankBuckets, orderIndex, rank, r, r + 1, childrenOf);
                }
            }
        }

        return orderIndex;
    }

    private static void ReorderRank(Dictionary<int, List<string>> rankBuckets, Dictionary<string, int> orderIndex, Dictionary<string, int> rank, int r, int neighborRank, Dictionary<string, IReadOnlyList<string>> neighborsOf) {
        if (!rankBuckets.TryGetValue(r, out var bucket)) {
            return;
        }

        var keyed = new (string Id, double Key)[bucket.Count];
        for (var i = 0; i < bucket.Count; i++) {
            var id = bucket[i];
            List<int> neighborOrders = neighborsOf.TryGetValue(id, out var neighbors)
                ? [.. neighbors.Where(n => rank[n] == neighborRank).Select(n => orderIndex[n]).OrderBy(x => x)]
                : [];
            keyed[i] = (id, neighborOrders.Count == 0 ? i : Median(neighborOrders));
        }

        var sorted = keyed.OrderBy(k => k.Key).ToList();
        for (var i = 0; i < sorted.Count; i++) {
            bucket[i] = sorted[i].Id;
            orderIndex[sorted[i].Id] = i;
        }
    }

    private const int AlignPasses = 4;
    private const double FreeWeight = 0.001;

    private static double MedianOf(List<double> sorted) {
        var n = sorted.Count;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[(n / 2) - 1] + sorted[n / 2]) / 2;
    }

    private static double[] Isotonic(List<string> bucket, double[] desired, double[] weight, Func<string, double> size, double gap) {
        var n = bucket.Count;
        var offset = new double[n];
        for (var i = 1; i < n; i++) {
            offset[i] = offset[i - 1] + size(bucket[i - 1]) + gap;
        }

        var blockValue = new List<double>(n);
        var blockWeight = new List<double>(n);
        var blockCount = new List<int>(n);
        for (var i = 0; i < n; i++) {
            blockValue.Add(desired[i] - offset[i]);
            blockWeight.Add(weight[i]);
            blockCount.Add(1);
            while (blockValue.Count > 1 && blockValue[^2] > blockValue[^1]) {
                var w = blockWeight[^2] + blockWeight[^1];
                var v = ((blockValue[^2] * blockWeight[^2]) + (blockValue[^1] * blockWeight[^1])) / w;
                var c = blockCount[^2] + blockCount[^1];
                blockValue.RemoveAt(blockValue.Count - 1);
                blockWeight.RemoveAt(blockWeight.Count - 1);
                blockCount.RemoveAt(blockCount.Count - 1);
                blockValue[^1] = v;
                blockWeight[^1] = w;
                blockCount[^1] = c;
            }
        }

        var placed = new double[n];
        var k = 0;
        for (var b = 0; b < blockValue.Count; b++) {
            for (var j = 0; j < blockCount[b]; j++, k++) {
                placed[k] = blockValue[b] + offset[k];
            }
        }
        return placed;
    }

    private static double Median(List<int> values) {
        var n = values.Count;
        if (n % 2 == 1) {
            return values[n / 2];
        }
        return (values[(n / 2) - 1] + values[n / 2]) / 2.0;
    }

    private static List<PositionedNode> ComputePositions(Dictionary<string, DiagramNode> nodesById, List<string> visibleOrder, Dictionary<string, int> rank, Dictionary<string, int> order, Dictionary<string, IReadOnlyList<DiagramEdge>> forwardByFrom, DiagramDirection direction, DiagramStyle style, HashSet<string> rootSet, Dictionary<string, int> hiddenChildren) {
        if (visibleOrder.Count == 0) {
            return [];
        }

        var vertical = direction is DiagramDirection.Down or DiagramDirection.Up;
        var mirror = direction is DiagramDirection.Up or DiagramDirection.Left;

        var width = new Dictionary<string, double>();
        var height = new Dictionary<string, double>();
        foreach (var id in visibleOrder) {
            var node = nodesById[id];
            width[id] = node.Width ?? style.NodeWidth;
            height[id] = node.Height ?? style.NodeHeight;
        }

        double PrimarySize(string id) {
            return vertical ? height[id] : width[id];
        }

        double CrossSize(string id) {
            return vertical ? width[id] : height[id];
        }

        var rankBuckets = new Dictionary<int, List<string>>();
        foreach (var id in visibleOrder) {
            var r = rank[id];
            if (!rankBuckets.TryGetValue(r, out var bucket)) {
                bucket = [];
                rankBuckets[r] = bucket;
            }
            bucket.Add(id);
        }
        foreach (var bucket in rankBuckets.Values) {
            bucket.Sort((a, b) => order[a].CompareTo(order[b]));
        }

        var minRank = rankBuckets.Keys.Min();
        var maxRank = rankBuckets.Keys.Max();

        var maxPrimaryByRank = new Dictionary<int, double>();
        foreach (var pair in rankBuckets) {
            maxPrimaryByRank[pair.Key] = pair.Value.Max(PrimarySize);
        }

        var primary = new Dictionary<string, double>();
        var offset = style.Padding;
        for (var r = minRank; r <= maxRank; r++) {
            foreach (var id in rankBuckets[r]) {
                primary[id] = offset;
            }
            offset += maxPrimaryByRank[r] + style.RankGap;
        }

        var cross = new Dictionary<string, double>();
        foreach (var bucket in rankBuckets.Values) {
            var c = style.Padding;
            foreach (var id in bucket) {
                cross[id] = c;
                c += CrossSize(id) + style.NodeGap;
            }
        }

        var childrenOf = new Dictionary<string, List<string>>();
        var parentsOf = new Dictionary<string, List<string>>();
        foreach (var pair in forwardByFrom) {
            foreach (var to in pair.Value.Select(e => e.To).Distinct()) {
                if (!childrenOf.TryGetValue(pair.Key, out var kids)) {
                    kids = [];
                    childrenOf[pair.Key] = kids;
                }
                kids.Add(to);
                if (!parentsOf.TryGetValue(to, out var parents)) {
                    parents = [];
                    parentsOf[to] = parents;
                }
                parents.Add(pair.Key);
            }
        }

        double Center(string id) {
            return cross[id] + (CrossSize(id) / 2);
        }

        void Align(List<string> bucket, Dictionary<string, List<string>> neighborsOf) {
            var desired = new double[bucket.Count];
            var weight = new double[bucket.Count];
            for (var i = 0; i < bucket.Count; i++) {
                var id = bucket[i];
                if (neighborsOf.TryGetValue(id, out var neighbors) && neighbors.Count > 0) {
                    List<double> centers = [.. neighbors.Select(Center).OrderBy(c => c)];
                    desired[i] = MedianOf(centers) - (CrossSize(id) / 2);
                    weight[i] = 1;
                } else {
                    desired[i] = cross[id];
                    weight[i] = FreeWeight;
                }
            }
            var placed = Isotonic(bucket, desired, weight, CrossSize, style.NodeGap);
            for (var i = 0; i < bucket.Count; i++) {
                cross[bucket[i]] = placed[i];
            }
        }

        for (var pass = 0; pass < AlignPasses; pass++) {
            for (var r = maxRank; r >= minRank; r--) {
                Align(rankBuckets[r], childrenOf);
            }
            for (var r = minRank; r <= maxRank; r++) {
                Align(rankBuckets[r], parentsOf);
            }
        }

        var minCross = visibleOrder.Min(id => cross[id]);
        var crossShift = style.Padding - minCross;
        foreach (var id in visibleOrder) {
            cross[id] += crossShift;
        }

        if (mirror) {
            var maxPrimaryExtent = visibleOrder.Max(id => primary[id] + PrimarySize(id));
            var total = maxPrimaryExtent + style.Padding;
            foreach (var id in visibleOrder) {
                primary[id] = total - primary[id] - PrimarySize(id);
            }
        }

        var positioned = new List<PositionedNode>(visibleOrder.Count);
        foreach (var id in visibleOrder) {
            var node = nodesById[id];
            var (x, y) = vertical ? (cross[id], primary[id]) : (primary[id], cross[id]);
            hiddenChildren.TryGetValue(id, out var hidden);
            positioned.Add(new PositionedNode(node, rank[id], order[id], x, y, width[id], height[id]) {
                HiddenChildren = hidden,
                IsRoot = rootSet.Contains(id),
            });
        }

        return positioned;
    }

    private static List<PositionedEdge> ComputeEdges(List<DiagramEdge> knownEdges, HashSet<string> visible, HashSet<DiagramEdge> backEdges, List<PositionedNode> positioned, DiagramDirection direction, DiagramStyle style) {
        var byId = new Dictionary<string, PositionedNode>();
        foreach (var node in positioned) {
            byId[node.Node.Id] = node;
        }

        var visibleEdges = knownEdges.Where(e => visible.Contains(e.From) && visible.Contains(e.To)).ToList();
        var maxWeight = 0.0;
        foreach (var edge in visibleEdges) {
            if (edge.Weight is double w && w > maxWeight) {
                maxWeight = w;
            }
        }

        var result = new List<PositionedEdge>(visibleEdges.Count);
        foreach (var edge in visibleEdges) {
            var source = byId[edge.From];
            var target = byId[edge.To];
            var (path, labelX, labelY) = BuildPath(source, target, direction);

            double edgeWidth;
            double opacity;
            if (edge.Weight is double weight) {
                var ratio = maxWeight <= 0 ? 0 : weight / maxWeight;
                edgeWidth = style.EdgeMinWidth + (ratio * (style.EdgeMaxWidth - style.EdgeMinWidth));
                opacity = style.EdgeMinOpacity + (ratio * (1 - style.EdgeMinOpacity));
            } else {
                edgeWidth = style.EdgeMinWidth;
                opacity = 1;
            }

            result.Add(new PositionedEdge(edge, path, edgeWidth, opacity, labelX, labelY) {
                IsBack = backEdges.Contains(edge),
            });
        }

        return result;
    }

    private static (string Path, double LabelX, double LabelY) BuildPath(PositionedNode source, PositionedNode target, DiagramDirection direction) {
        var (ex, ey) = ExitPoint(source, direction);
        var (tx, ty) = EntryPoint(target, direction);
        var vertical = direction is DiagramDirection.Down or DiagramDirection.Up;

        double c1x, c1y, c2x, c2y;
        if (vertical) {
            var half = (ty - ey) / 2;
            c1x = ex;
            c1y = ey + half;
            c2x = tx;
            c2y = ty - half;
        } else {
            var half = (tx - ex) / 2;
            c1x = ex + half;
            c1y = ey;
            c2x = tx - half;
            c2y = ty;
        }

        var path = $"M {Fmt(ex)},{Fmt(ey)} C {Fmt(c1x)},{Fmt(c1y)} {Fmt(c2x)},{Fmt(c2y)} {Fmt(tx)},{Fmt(ty)}";
        var labelX = CubicAt(ex, c1x, c2x, tx);
        var labelY = CubicAt(ey, c1y, c2y, ty);

        return (path, labelX, labelY);
    }

    private static double CubicAt(double p0, double p1, double p2, double p3) => (0.125 * p0) + (0.375 * p1) + (0.375 * p2) + (0.125 * p3);

    private static string Fmt(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static (double X, double Y) ExitPoint(PositionedNode node, DiagramDirection direction) => direction switch {
        DiagramDirection.Down => (node.X + (node.Width / 2), node.Y + node.Height),
        DiagramDirection.Up => (node.X + (node.Width / 2), node.Y),
        DiagramDirection.Right => (node.X + node.Width, node.Y + (node.Height / 2)),
        DiagramDirection.Left => (node.X, node.Y + (node.Height / 2)),
        _ => (node.X + (node.Width / 2), node.Y + node.Height),
    };

    private static (double X, double Y) EntryPoint(PositionedNode node, DiagramDirection direction) => direction switch {
        DiagramDirection.Down => (node.X + (node.Width / 2), node.Y),
        DiagramDirection.Up => (node.X + (node.Width / 2), node.Y + node.Height),
        DiagramDirection.Right => (node.X, node.Y + (node.Height / 2)),
        DiagramDirection.Left => (node.X + node.Width, node.Y + (node.Height / 2)),
        _ => (node.X + (node.Width / 2), node.Y),
    };
}
