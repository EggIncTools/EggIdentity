namespace EggIdentity.Reports;

public sealed record ChartStyle {
    public static ChartStyle Default { get; } = new();

    public double Width { get; init; } = 300;
    public double Height { get; init; } = 150;
    public double PadLeft { get; init; } = 36;
    public double PadRight { get; init; } = 8;
    public double PadTop { get; init; } = 10;
    public double PadBottom { get; init; } = 60;
    public double TickInset { get; init; } = 4;
    public int MaxLabels { get; init; } = 12;
    public IReadOnlyList<double> TickFractions { get; init; } = [0.33, 0.67, 1.0];
    public int SeriesSlots { get; init; } = 8;
    public double PieSize { get; init; } = 240;
    public double PieRadius { get; init; } = 0.36;
    public double PieLabelInset { get; init; } = 0.6;
    public int PieMaxSegments { get; init; } = 10;
    public int GroupedMaxCols { get; init; } = 11;
    public double HeatFloor { get; init; } = 0.12;
    public double HeatRatioSpan { get; init; } = 2;
    public double ContrastLuminance { get; init; } = 0.179;
    public double SeriesLightness { get; init; } = 0.55;
    public double HeatStrongAt { get; init; } = 0.6;
    public string? HeatLow { get; init; }
    public string? HeatHigh { get; init; }
    public string? SeriesBase { get; init; }

    public double PlotWidth => Width - PadLeft - PadRight;
    public double PlotHeight => Height - PadTop - PadBottom;
    public double Baseline => PadTop + PlotHeight;
}
