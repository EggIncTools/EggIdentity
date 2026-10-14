namespace EggIdentity.Diagrams;

public sealed record PositionedNode(DiagramNode Node, int Rank, int Order, double X, double Y, double Width, double Height) {
    public int HiddenChildren { get; init; }
    public bool IsRoot { get; init; }
}

public sealed record PositionedEdge(DiagramEdge Edge, string Path, double Width, double Opacity, double LabelX, double LabelY) {
    public bool IsBack { get; init; }
}

public sealed record DiagramLayout(double Width, double Height, IReadOnlyList<PositionedNode> Nodes, IReadOnlyList<PositionedEdge> Edges) {
    public PositionedNode? Node(string id) {
        ArgumentNullException.ThrowIfNull(id);
        foreach (var node in Nodes) {
            if (node.Node.Id == id) {
                return node;
            }
        }
        return null;
    }
}
