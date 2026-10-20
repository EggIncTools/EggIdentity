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

        var splitLayout = TryBuildSplitLayout(graph, nodesById, known, knownEdges, roots, direction, style, collapsedSet);
        if (splitLayout != null) {
            return splitLayout;
        }

        var (positioned, backEdges) = ComputeLayoutCore(graph, nodesById, known, roots, direction, style, collapsedSet);
        var positionedEdges = ComputeEdges(knownEdges, backEdges, positioned, style);

        return BuildLayout(positioned, positionedEdges, style);
    }

    private static DiagramLayout BuildLayout(List<PositionedNode> positioned, List<PositionedEdge> edges, DiagramStyle style) {
        var width = positioned.Count == 0 ? 0 : positioned.Max(n => n.X + n.Width) + style.Padding;
        var height = positioned.Count == 0 ? 0 : positioned.Max(n => n.Y + n.Height) + style.Padding;
        return new DiagramLayout(width, height, positioned, edges);
    }

    private static (List<PositionedNode> Positioned, HashSet<DiagramEdge> BackEdges) ComputeLayoutCore(DiagramGraph graph, Dictionary<string, DiagramNode> nodesById, HashSet<string> known, IReadOnlyList<string> roots, DiagramDirection direction, DiagramStyle style, IReadOnlySet<string> collapsed) {
        var knownEdges = graph.Edges.Where(e => known.Contains(e.From) && known.Contains(e.To)).ToList();
        var edgesByFrom = knownEdges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => (IReadOnlyList<DiagramEdge>)[.. g]);

        var (visibleOrder, visibleSet, rootSet) = ComputeVisible(roots, known, edgesByFrom, collapsed);
        var hiddenChildren = ComputeHiddenChildren(graph, edgesByFrom, visibleSet, collapsed);
        var backEdges = ComputeBackEdges(roots, visibleSet, edgesByFrom);
        var forwardEdges = knownEdges.Where(e => visibleSet.Contains(e.From) && visibleSet.Contains(e.To) && !backEdges.Contains(e)).ToList();
        var forwardByFrom = forwardEdges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => (IReadOnlyList<DiagramEdge>)[.. g]);

        var rank = ComputeRanks(visibleOrder, forwardEdges, rootSet);
        var order = ComputeOrder(visibleOrder, rank, forwardEdges, style.Sweeps);
        var positioned = ComputePositions(nodesById, visibleOrder, rank, order, forwardByFrom, direction, style, rootSet, hiddenChildren);

        return (positioned, backEdges);
    }

    private static DiagramLayout? TryBuildSplitLayout(DiagramGraph graph, Dictionary<string, DiagramNode> nodesById, HashSet<string> known, List<DiagramEdge> knownEdges, IReadOnlyList<string> roots, DiagramDirection direction, DiagramStyle style, IReadOnlySet<string> collapsed) {
        if (!style.SplitRoot) {
            return null;
        }

        var rootCandidates = roots.Where(known.Contains).Distinct().ToList();
        if (rootCandidates.Count != 1) {
            return null;
        }

        var root = rootCandidates[0];
        if (collapsed.Contains(root)) {
            return null;
        }

        var edgesByFrom = knownEdges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => (IReadOnlyList<DiagramEdge>)[.. g]);
        var children = GetRootChildren(root, edgesByFrom);
        if (children.Count < 2) {
            return null;
        }

        return BuildSplitLayout(graph, nodesById, knownEdges, edgesByFrom, root, children, direction, style, collapsed);
    }

    private static List<string> GetRootChildren(string root, Dictionary<string, IReadOnlyList<DiagramEdge>> edgesByFrom) {
        var children = new List<string>();
        if (!edgesByFrom.TryGetValue(root, out var outgoing)) {
            return children;
        }

        var seen = new HashSet<string>();
        foreach (var edge in outgoing) {
            if (edge.To == root) {
                continue;
            }
            if (seen.Add(edge.To)) {
                children.Add(edge.To);
            }
        }

        return children;
    }

    private static Dictionary<string, int> ComputeOwnership(string root, List<string> children, Dictionary<string, IReadOnlyList<DiagramEdge>> edgesByFrom, IReadOnlySet<string> collapsed) {
        var owner = new Dictionary<string, int>();
        var queue = new Queue<string>();

        for (var i = 0; i < children.Count; i++) {
            var child = children[i];
            if (child == root) {
                continue;
            }
            var side = i % 2;
            if (owner.TryAdd(child, side)) {
                queue.Enqueue(child);
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
            var side = owner[id];
            foreach (var edge in outgoing) {
                if (edge.To == root) {
                    continue;
                }
                if (owner.TryAdd(edge.To, side)) {
                    queue.Enqueue(edge.To);
                }
            }
        }

        return owner;
    }

    private static HashSet<string> BuildSideSet(string root, Dictionary<string, int> owner, int side) {
        var set = new HashSet<string> { root };
        foreach (var pair in owner) {
            if (pair.Value == side) {
                set.Add(pair.Key);
            }
        }
        return set;
    }

    private static DiagramLayout BuildSplitLayout(DiagramGraph graph, Dictionary<string, DiagramNode> nodesById, List<DiagramEdge> knownEdges, Dictionary<string, IReadOnlyList<DiagramEdge>> edgesByFrom, string root, List<string> children, DiagramDirection direction, DiagramStyle style, IReadOnlySet<string> collapsed) {
        var owner = ComputeOwnership(root, children, edgesByFrom, collapsed);
        var primarySet = BuildSideSet(root, owner, 0);
        var mirrorSet = BuildSideSet(root, owner, 1);
        var opposite = Opposite(direction);
        var rootOnly = new List<string> { root };

        var (primaryPositioned, primaryBack) = ComputeLayoutCore(graph, nodesById, primarySet, rootOnly, direction, style, collapsed);
        var (mirrorPositioned, mirrorBack) = ComputeLayoutCore(graph, nodesById, mirrorSet, rootOnly, opposite, style, collapsed);

        var combinedNodes = CombineSides(primaryPositioned, mirrorPositioned, root);
        var positioned = ShiftToPadding(combinedNodes, style);

        var positionedIds = new HashSet<string>(positioned.Select(n => n.Node.Id));
        var combinedKnownEdges = knownEdges.Where(e => positionedIds.Contains(e.From) && positionedIds.Contains(e.To)).ToList();
        var combinedBack = CombineBackEdges(combinedKnownEdges, primaryBack, mirrorBack, owner, root);

        var positionedEdges = ComputeEdges(combinedKnownEdges, combinedBack, positioned, style);
        return BuildLayout(positioned, positionedEdges, style);
    }

    private static List<PositionedNode> CombineSides(List<PositionedNode> primary, List<PositionedNode> mirror, string root) {
        var primaryRoot = primary.First(n => n.Node.Id == root);
        var mirrorRoot = mirror.First(n => n.Node.Id == root);
        var dx = primaryRoot.X - mirrorRoot.X;
        var dy = primaryRoot.Y - mirrorRoot.Y;

        var combined = new List<PositionedNode>(primary.Count + mirror.Count - 1);
        combined.AddRange(primary);
        foreach (var node in mirror) {
            if (node.Node.Id == root) {
                continue;
            }
            combined.Add(node with {
                X = node.X + dx,
                Y = node.Y + dy,
                Rank = -node.Rank,
            });
        }

        return combined;
    }

    private static List<PositionedNode> ShiftToPadding(List<PositionedNode> nodes, DiagramStyle style) {
        if (nodes.Count == 0) {
            return nodes;
        }

        var minX = nodes.Min(n => n.X);
        var minY = nodes.Min(n => n.Y);
        var dx = style.Padding - minX;
        var dy = style.Padding - minY;

        var shifted = new List<PositionedNode>(nodes.Count);
        foreach (var node in nodes) {
            shifted.Add(node with { X = node.X + dx, Y = node.Y + dy });
        }
        return shifted;
    }

    private static HashSet<DiagramEdge> CombineBackEdges(List<DiagramEdge> combinedKnownEdges, HashSet<DiagramEdge> primaryBack, HashSet<DiagramEdge> mirrorBack, Dictionary<string, int> owner, string root) {
        var combined = new HashSet<DiagramEdge>(primaryBack);
        foreach (var edge in mirrorBack) {
            combined.Add(edge);
        }

        foreach (var edge in combinedKnownEdges) {
            if (edge.From == root || edge.To == root) {
                continue;
            }
            if (owner.TryGetValue(edge.From, out var fromSide) && owner.TryGetValue(edge.To, out var toSide) && fromSide != toSide) {
                combined.Add(edge);
            }
        }

        return combined;
    }

    private static DiagramDirection Opposite(DiagramDirection direction) => direction switch {
        DiagramDirection.Down => DiagramDirection.Up,
        DiagramDirection.Up => DiagramDirection.Down,
        DiagramDirection.Right => DiagramDirection.Left,
        DiagramDirection.Left => DiagramDirection.Right,
        _ => DiagramDirection.Up,
    };

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
                Direction = direction,
            });
        }

        return positioned;
    }

    private static List<PositionedEdge> ComputeEdges(List<DiagramEdge> knownEdges, HashSet<DiagramEdge> backEdges, List<PositionedNode> positioned, DiagramStyle style) {
        var byId = new Dictionary<string, PositionedNode>();
        foreach (var node in positioned) {
            byId[node.Node.Id] = node;
        }

        var visibleEdges = knownEdges.Where(e => byId.ContainsKey(e.From) && byId.ContainsKey(e.To)).ToList();
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
            var edgeDirection = source.IsRoot ? target.Direction : source.Direction;
            var (path, labelX, labelY) = BuildPath(source, target, edgeDirection, target.Direction, style);

            var (edgeWidth, opacity) = EdgeScale(edge, maxWeight, style);
            result.Add(new PositionedEdge(edge, path, edgeWidth, opacity, labelX, labelY) {
                IsBack = backEdges.Contains(edge),
                Direction = edgeDirection,
            });
        }

        return result;
    }

    private static (string Path, double LabelX, double LabelY) BuildPath(PositionedNode source, PositionedNode target, DiagramDirection exitDirection, DiagramDirection entryDirection, DiagramStyle style) {
        var (ex, ey) = ExitPoint(source, exitDirection);
        var (tx, ty) = EntryPoint(target, entryDirection);

        double c1x, c1y, c2x, c2y;
        if (exitDirection == entryDirection) {
            var vertical = exitDirection is DiagramDirection.Down or DiagramDirection.Up;
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
        } else {
            var (exUx, exUy) = Unit(exitDirection);
            var (enUx, enUy) = Unit(entryDirection);
            var halfGap = style.RankGap / 2;
            c1x = ex + (halfGap * exUx);
            c1y = ey + (halfGap * exUy);
            c2x = tx - (halfGap * enUx);
            c2y = ty - (halfGap * enUy);
        }

        var path = $"M {Fmt(ex)},{Fmt(ey)} C {Fmt(c1x)},{Fmt(c1y)} {Fmt(c2x)},{Fmt(c2y)} {Fmt(tx)},{Fmt(ty)}";
        var labelX = CubicAt(ex, c1x, c2x, tx);
        var labelY = CubicAt(ey, c1y, c2y, ty);

        return (path, labelX, labelY);
    }

    private static (double Width, double Opacity) EdgeScale(DiagramEdge edge, double maxWeight, DiagramStyle style) {
        if (edge.Weight is not double weight) {
            return (style.EdgeMinWidth, 1);
        }
        var ratio = maxWeight <= 0 ? 0 : weight / maxWeight;
        return (style.EdgeMinWidth + (ratio * (style.EdgeMaxWidth - style.EdgeMinWidth)), style.EdgeMinOpacity + (ratio * (1 - style.EdgeMinOpacity)));
    }

    private static (double X, double Y) Unit(DiagramDirection direction) => direction switch {
        DiagramDirection.Down => (0, 1),
        DiagramDirection.Up => (0, -1),
        DiagramDirection.Right => (1, 0),
        DiagramDirection.Left => (-1, 0),
        _ => (0, 1),
    };

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
