using System.Diagnostics.CodeAnalysis;
using EggIdentity.Reports;
using Microsoft.AspNetCore.Components;

namespace EggIdentity.UI.Reports;

public sealed class ReportRenderers {
    private readonly Dictionary<string, Type> _map = [];

    public static ReportRenderers Default => new ReportRenderers()
        .Add(ReportCharts.Bar, typeof(BarChart))
        .Add(ReportCharts.Line, typeof(LineChart))
        .Add(ReportCharts.Pie, typeof(PieChart))
        .Add(ReportCharts.Heatmap, typeof(HeatmapChart))
        .Add(ReportCharts.GroupedBar, typeof(GroupedBarChart))
        .Add(ReportCharts.MultiLine, typeof(MultiLineChart))
        .Add(ReportCharts.Table, typeof(TableChart));

    public ReportRenderers Add(string key, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type component) {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(component);
        if (!typeof(IComponent).IsAssignableFrom(component))
            throw new ArgumentException($"{component} does not implement IComponent", nameof(component));
        _map[key] = component;
        return this;
    }

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    [UnconditionalSuppressMessage("Trimming", "IL2073", Justification = "every stored type entered through the annotated Add parameter")]
    public Type? Find(string key) {
        ArgumentNullException.ThrowIfNull(key);
        return _map.GetValueOrDefault(key);
    }
}
