using EggIdentity.Reports;

namespace EggIdentity.UI.Reports;

public sealed record ReportBuilderLabels {
    public static ReportBuilderLabels Default { get; } = new();

    public string Title { get; init; } = "Title";
    public string TitlePlaceholder { get; init; } = "Report title";
    public string Description { get; init; } = "Description";
    public string DescriptionPlaceholder { get; init; } = "What does this report show?";

    public string Source { get; init; } = "Source";
    public string SourceSelect { get; init; } = "Select a source...";

    public string GroupBy { get; init; } = "Group by";
    public string GroupByAdd { get; init; } = "Add group";
    public string GroupByRemove { get; init; } = "Remove group";
    public string GroupByDimension { get; init; } = "Dimension...";
    public string GroupByBucket { get; init; } = "Bucket";
    public string FillGaps { get; init; } = "Fill empty buckets";

    public string Measures { get; init; } = "Measures";
    public string MeasuresAdd { get; init; } = "Add measure";
    public string MeasuresRemove { get; init; } = "Remove measure";
    public string RowCount { get; init; } = "Row count";

    public string Filters { get; init; } = "Filters";

    public string Normalize { get; init; } = "Normalize";
    public string NormalizeNone { get; init; } = "None";
    public string NormalizeRow { get; init; } = "Row";
    public string NormalizeColumn { get; init; } = "Column";
    public string NormalizeGlobal { get; init; } = "Global";
    public string NormalizeBy { get; init; } = "By measure";

    public string Sort { get; init; } = "Sort";
    public string SortAdd { get; init; } = "Add sort key";
    public string SortRemove { get; init; } = "Remove sort key";
    public string SortKeySelect { get; init; } = "Key...";
    public string SortDescending { get; init; } = "Descending";

    public string Limit { get; init; } = "Limit";
    public string LimitPlaceholder { get; init; } = "No limit";

    public string Having { get; init; } = "Having";
    public string HavingAdd { get; init; } = "Add condition";
    public string HavingRemove { get; init; } = "Remove condition";
    public string HavingMeasure { get; init; } = "Measure...";
    public string HavingThreshold { get; init; } = "Threshold";

    public string MinSample { get; init; } = "Minimum sample";
    public string SampleRule { get; init; } = "Below minimum";
    public string SampleShow { get; init; } = "Show";
    public string SampleDim { get; init; } = "Dim";
    public string SampleHide { get; init; } = "Hide";

    public string Chart { get; init; } = "Chart";
    public string Unit { get; init; } = "Unit";
    public string UnitPlaceholder { get; init; } = "e.g. per launch";
    public string Fill { get; init; } = "Fill area";

    public string Appearance { get; init; } = "Appearance";
    public string Color { get; init; } = "Color";
    public string Unfilled { get; init; } = "Unfilled color";
    public string LabelColors { get; init; } = "Value colors";

    public string Cost { get; init; } = "Cost";

    public string Running { get; init; } = "Running...";
    public string Empty { get; init; } = "No data";

    public string MoveUp { get; init; } = "Move up";
    public string MoveDown { get; init; } = "Move down";
    public string Width { get; init; } = "Width";
    public string Height { get; init; } = "Height";

    public string ChartLabel(string key) => key switch {
        ReportCharts.Bar => "Bar chart",
        ReportCharts.Line => "Line chart",
        ReportCharts.Pie => "Pie chart",
        ReportCharts.Heatmap => "Heatmap",
        ReportCharts.GroupedBar => "Grouped bar",
        ReportCharts.MultiLine => "Multi-line",
        ReportCharts.Table => "Table",
        _ => key,
    };

    public string AggregationLabel(Aggregation aggregation) => aggregation switch {
        Aggregation.Count => "Count",
        Aggregation.Sum => "Sum",
        Aggregation.Avg => "Average",
        Aggregation.Min => "Min",
        Aggregation.Max => "Max",
        Aggregation.Distinct => "Distinct",
        _ => aggregation.ToString(),
    };
}
