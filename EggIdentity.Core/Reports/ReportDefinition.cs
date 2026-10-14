using System.Text.Json.Nodes;

namespace EggIdentity.Reports;

public static class ReportCharts {
    public const string Bar = "bar";
    public const string Line = "line";
    public const string Pie = "pie";
    public const string Heatmap = "heatmap";
    public const string GroupedBar = "grouped_bar";
    public const string MultiLine = "multi_line";
    public const string Table = "table";

    public static IReadOnlyList<string> BuiltIn { get; } = [Bar, Line, Pie, Heatmap, GroupedBar, MultiLine, Table];
}

public enum HeatScale {
    Auto,
    Relative,
    Ratio,
}

public sealed record HeatView(string? ColorBy = null, string? SecondaryBy = null, HeatScale Scale = HeatScale.Auto);

public sealed record ReportView(string Chart) {
    public SampleRule Sample { get; init; } = SampleRule.Show;
    public string? Unit { get; init; }
    public bool Fill { get; init; } = true;
    public HeatView? Heat { get; init; }
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public string? Palette { get; init; }
    public string? Color { get; init; }
    public IReadOnlyDictionary<string, string> LabelColors { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    public string? Unfilled { get; init; }
    public IReadOnlyDictionary<string, string> Options { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record ReportDefinition(int Version, string Id, string Title, ReportQuery Query, ReportView View) {
    public string? Description { get; init; }
}

public sealed record ReportPlacement(string ReportId, int X, int Y, int W, int H, int Order) {
    public string? Group { get; init; }
}

public sealed record LegacySplit(JsonObject Definition, ReportPlacement? Placement);

public sealed class ReportUpgrades {
    private readonly Dictionary<int, Func<JsonObject, LegacySplit>> _steps = [];

    public ReportUpgrades Add(int fromVersion, Func<JsonObject, LegacySplit> step) {
        ArgumentOutOfRangeException.ThrowIfNegative(fromVersion);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fromVersion, ReportCodec.CurrentVersion);
        ArgumentNullException.ThrowIfNull(step);
        _steps[fromVersion] = step;
        return this;
    }

    public Func<JsonObject, LegacySplit>? Step(int fromVersion) => _steps.GetValueOrDefault(fromVersion);
}
