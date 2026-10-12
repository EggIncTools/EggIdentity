using EggIdentity.Filters;

namespace EggIdentity.UI;

public sealed record FilterBuilderLabels {
    public static FilterBuilderLabels Default { get; } = new();

    public string And { get; init; } = "AND";
    public string Or { get; init; } = "OR";
    public string AddOr { get; init; } = "OR";
    public string AddOrTooltip { get; init; } = "Add alternate filter condition";
    public string RemoveAnd { get; init; } = "Remove filter condition";
    public string RemoveOr { get; init; } = "Remove alternate condition";
    public string Field { get; init; } = "Field...";
    public string Select { get; init; } = "Select...";
    public string ClickToSelect { get; init; } = "Click to select";
    public string Incomplete { get; init; } = "Fill in the missing field";
    public string Is { get; init; } = "is";
}

public sealed record FilterValueEditorContext(
    FilterFieldDef Field,
    string Value,
    IReadOnlyList<FilterOption> Options,
    bool Incomplete,
    Func<string?, Task> SetValue);
