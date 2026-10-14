namespace EggIdentity.UI.Reports;

public sealed record ReportLabels {
    public static ReportLabels Default { get; } = new();

    public string EmptyTitle { get; init; } = "No data";
    public string EmptyDescription { get; init; } = "Nothing matches the current filters.";
    public string UnknownChart { get; init; } = "Unknown chart type";
    public string Total { get; init; } = "Total";
    public string SampleCountPrefix { get; init; } = "n=";
    public string BelowSampleTooltip { get; init; } = "Below the minimum sample size";
    public string TableHeaderFallback { get; init; } = "Value";
    public string Other { get; init; } = "Other";
}
