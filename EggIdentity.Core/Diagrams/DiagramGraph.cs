namespace EggIdentity.Diagrams;

public sealed record DiagramNode(string Id) {
    public double? Width { get; init; }
    public double? Height { get; init; }
}

public sealed record DiagramEdge(string From, string To) {
    public double? Weight { get; init; }
    public string? Label { get; init; }
}

public enum DiagramDirection {
    Down,
    Up,
    Right,
    Left
}

public sealed record DiagramGraph(IReadOnlyList<DiagramNode> Nodes, IReadOnlyList<DiagramEdge> Edges) {
    public DiagramGraph Reverse() {
        return new DiagramGraph(Nodes, [.. Edges.Select(e => e with { From = e.To, To = e.From })]);
    }

    public DiagramNode? Node(string id) {
        ArgumentNullException.ThrowIfNull(id);
        foreach (var node in Nodes) {
            if (node.Id == id) {
                return node;
            }
        }
        return null;
    }
}
