namespace EggIdentity.UI.Diagrams;

public sealed record DiagramLabels {
    public static DiagramLabels Default { get; } = new();

    public string ZoomIn { get; init; } = "Zoom in";
    public string ZoomOut { get; init; } = "Zoom out";
    public string Fit { get; init; } = "Fit to view";
    public string Expand { get; init; } = "Expand";
    public string Collapse { get; init; } = "Collapse";

    public string ToggleAriaLabel(string nodeAriaLabel, bool collapsed) => collapsed
        ? $"{Expand} {nodeAriaLabel}"
        : $"{Collapse} {nodeAriaLabel}";
}
