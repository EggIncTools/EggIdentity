namespace EggIdentity.Diagrams;

public sealed record DiagramStyle {
    public double NodeWidth { get; init; } = 140;
    public double NodeHeight { get; init; } = 48;
    public double RankGap { get; init; } = 72;
    public double NodeGap { get; init; } = 24;
    public double Padding { get; init; } = 16;
    public double EdgeMinWidth { get; init; } = 1;
    public double EdgeMaxWidth { get; init; } = 8;
    public double EdgeMinOpacity { get; init; } = 0.35;
    public int Sweeps { get; init; } = 4;
    public IReadOnlyList<double> ZoomSteps { get; init; } = [0.5, 0.75, 1, 1.25, 1.5, 2];

    public static DiagramStyle Default { get; } = new();
}
