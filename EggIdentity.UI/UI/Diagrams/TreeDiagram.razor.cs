using System.Globalization;
using EggIdentity.Diagrams;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace EggIdentity.UI.Diagrams;

public sealed partial class TreeDiagram : ComponentBase {
    private static readonly HashSet<string> ArrowKeys = ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"];

    private readonly Dictionary<string, ElementReference> _nodeRefs = [];
    private DiagramGraph? _layoutGraph;
    private IReadOnlyList<string>? _layoutRoots;
    private DiagramDirection _layoutDirection;
    private DiagramStyle? _layoutStyle;
    private IReadOnlySet<string>? _layoutCollapsed;
    private DiagramLayout _layout = new(0, 0, [], []);
    private int _zoomIndex;
    private bool _preventArrowDefault;

    [Parameter, EditorRequired] public DiagramGraph Graph { get; set; } = default!;
    [Parameter] public IReadOnlyList<string> Roots { get; set; } = [];
    [Parameter] public DiagramDirection Direction { get; set; } = DiagramDirection.Down;
    [Parameter] public DiagramStyle? Style { get; set; }
    [Parameter] public IReadOnlySet<string> Collapsed { get; set; } = new HashSet<string>();
    [Parameter] public EventCallback<IReadOnlySet<string>> CollapsedChanged { get; set; }
    [Parameter] public string? SelectedId { get; set; }
    [Parameter] public EventCallback<string?> SelectedIdChanged { get; set; }
    [Parameter, EditorRequired] public RenderFragment<PositionedNode> NodeTemplate { get; set; } = default!;
    [Parameter, EditorRequired] public Func<DiagramNode, string> AriaLabel { get; set; } = default!;
    [Parameter] public Func<PositionedEdge, string?>? EdgeClass { get; set; }
    [Parameter] public Func<PositionedEdge, string?>? EdgeStyle { get; set; }
    [Parameter] public RenderFragment<PositionedEdge>? EdgeLabelTemplate { get; set; }
    [Parameter] public Func<PositionedNode, string?>? NodeClass { get; set; }
    [Parameter] public DiagramLabels Labels { get; set; } = DiagramLabels.Default;

    private DiagramStyle EffectiveStyle => Style ?? DiagramStyle.Default;

    private double Zoom {
        get {
            var steps = EffectiveStyle.ZoomSteps;
            if (steps.Count == 0) {
                return 1;
            }
            return steps[Math.Clamp(_zoomIndex, 0, steps.Count - 1)];
        }
    }

    protected override void OnParametersSet() {
        if (ReferenceEquals(Graph, _layoutGraph)
            && ReferenceEquals(Roots, _layoutRoots)
            && Direction == _layoutDirection
            && ReferenceEquals(Style, _layoutStyle)
            && ReferenceEquals(Collapsed, _layoutCollapsed)) {
            return;
        }

        _layoutGraph = Graph;
        _layoutRoots = Roots;
        _layoutDirection = Direction;
        _layoutStyle = Style;
        _layoutCollapsed = Collapsed;
        _layout = LayeredLayout.Layout(Graph, Roots, Direction, EffectiveStyle, Collapsed);
        ResetZoomIndex();
    }

    private void ResetZoomIndex() {
        var steps = EffectiveStyle.ZoomSteps;
        var index = steps.ToList().IndexOf(1);
        _zoomIndex = index >= 0 ? index : 0;
    }

    private void ZoomIn() {
        var steps = EffectiveStyle.ZoomSteps;
        if (steps.Count == 0) {
            return;
        }
        _zoomIndex = Math.Min(_zoomIndex + 1, steps.Count - 1);
    }

    private void ZoomOut() {
        if (EffectiveStyle.ZoomSteps.Count == 0) {
            return;
        }
        _zoomIndex = Math.Max(_zoomIndex - 1, 0);
    }

    private void Fit() => ResetZoomIndex();

    private bool CanZoomOut => _zoomIndex > 0;

    private bool CanZoomIn => _zoomIndex < EffectiveStyle.ZoomSteps.Count - 1;

    private bool HasChildren(string nodeId) => Graph.Edges.Any(e => e.From == nodeId);

    private bool IsCollapsed(string nodeId) => Collapsed.Contains(nodeId);

    private bool IsSelected(string nodeId) => SelectedId == nodeId;

    private string ToggleStyle(PositionedNode node) {
        var (x, y) = Direction switch {
            DiagramDirection.Down => (node.X + (node.Width / 2), node.Y + node.Height),
            DiagramDirection.Up => (node.X + (node.Width / 2), node.Y),
            DiagramDirection.Right => (node.X + node.Width, node.Y + (node.Height / 2)),
            DiagramDirection.Left => (node.X, node.Y + (node.Height / 2)),
            _ => (node.X + (node.Width / 2), node.Y + node.Height),
        };

        return string.Create(CultureInfo.InvariantCulture, $"left:{x:0.##}px;top:{y:0.##}px;transform:translate(-50%,-50%)");
    }

    private async Task SelectAsync(string id) {
        if (SelectedId == id) {
            return;
        }
        SelectedId = id;
        await SelectedIdChanged.InvokeAsync(id);
    }

    private async Task ToggleCollapsedAsync(string id) {
        var next = new HashSet<string>(Collapsed);
        if (!next.Remove(id)) {
            next.Add(id);
        }
        Collapsed = next;
        await CollapsedChanged.InvokeAsync(next);
    }

    private async Task OnNodeKeyDown(string nodeId, KeyboardEventArgs e) {
        _preventArrowDefault = ArrowKeys.Contains(e.Key);
        if (!_preventArrowDefault) {
            return;
        }

        var node = _layout.Node(nodeId);
        if (node is null) {
            return;
        }

        var targetId = ResolveArrowTarget(node, e.Key);
        if (targetId is not null && _nodeRefs.TryGetValue(targetId, out var elementRef)) {
            await elementRef.FocusAsync();
        }
    }

    private string? ResolveArrowTarget(PositionedNode node, string key) {
        var (childKey, parentKey) = Direction switch {
            DiagramDirection.Down => ("ArrowDown", "ArrowUp"),
            DiagramDirection.Up => ("ArrowUp", "ArrowDown"),
            DiagramDirection.Right => ("ArrowRight", "ArrowLeft"),
            DiagramDirection.Left => ("ArrowLeft", "ArrowRight"),
            _ => ("ArrowDown", "ArrowUp"),
        };

        if (key == childKey) {
            return FindChild(node.Node.Id);
        }
        if (key == parentKey) {
            return FindParent(node.Node.Id);
        }

        var verticalRank = Direction is DiagramDirection.Down or DiagramDirection.Up;
        var orderDelta = verticalRank
            ? key switch { "ArrowLeft" => -1, "ArrowRight" => 1, _ => 0 }
            : key switch { "ArrowUp" => -1, "ArrowDown" => 1, _ => 0 };

        return orderDelta == 0 ? null : FindSameRank(node, orderDelta);
    }

    private string? FindParent(string nodeId) => _layout.Edges.FirstOrDefault(e => e.Edge.To == nodeId && !e.IsBack)?.Edge.From;

    private string? FindChild(string nodeId) => _layout.Edges.FirstOrDefault(e => e.Edge.From == nodeId && !e.IsBack)?.Edge.To;

    private string? FindSameRank(PositionedNode node, int delta) {
        var sameRank = _layout.Nodes.Where(n => n.Rank == node.Rank).OrderBy(n => n.Order).ToList();
        var index = sameRank.FindIndex(n => n.Node.Id == node.Node.Id);
        if (index < 0) {
            return null;
        }
        var next = index + delta;
        return next >= 0 && next < sameRank.Count ? sameRank[next].Node.Id : null;
    }
}
