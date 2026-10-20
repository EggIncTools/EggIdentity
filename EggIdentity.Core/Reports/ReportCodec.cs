using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using EggIdentity.Filters;

namespace EggIdentity.Reports;

public static class ReportCodec {
    public const int CurrentVersion = 1;
    private const string VersionField = "version";

    public static JsonSerializerOptions Options => ReportJsonContext.Default.Options;

    public static string Serialize(ReportDefinition definition) {
        ArgumentNullException.ThrowIfNull(definition);
        return JsonSerializer.Serialize(Clean(definition with { Version = CurrentVersion }), ReportJsonContext.Default.ReportDefinition);
    }

    public static ReportDefinition Deserialize(string json) => Upgrade(json, new ReportUpgrades()).Definition;

    public static (ReportDefinition Definition, ReportPlacement? Placement) Upgrade(string json, ReportUpgrades upgrades) {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return Upgrade(JsonNode.Parse(json) as JsonObject ?? throw new JsonException("report definition must be an object"), upgrades);
    }

    public static (ReportDefinition Definition, ReportPlacement? Placement) Upgrade(JsonObject json, ReportUpgrades upgrades) {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(upgrades);
        var current = json;
        ReportPlacement? placement = null;
        var version = Version(current);
        while (version < CurrentVersion) {
            var step = upgrades.Step(version) ?? throw new JsonException($"no upgrade step from report version {version}");
            var split = step(current);
            current = split.Definition;
            placement = split.Placement ?? placement;
            var next = Version(current);
            version = next > version ? next : version + 1;
            current[VersionField] = version;
        }
        if (version > CurrentVersion) throw new JsonException($"report version {version} is newer than {CurrentVersion}");
        var definition = current.Deserialize(ReportJsonContext.Default.ReportDefinition) ?? throw new JsonException("report definition is null");
        return (Clean(definition), placement);
    }

    public static string SerializePlacement(ReportPlacement placement) {
        ArgumentNullException.ThrowIfNull(placement);
        return JsonSerializer.Serialize(placement, ReportJsonContext.Default.ReportPlacement);
    }

    public static ReportPlacement? DeserializePlacement(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize(json, ReportJsonContext.Default.ReportPlacement);

    public static bool IsHex([NotNullWhen(true)] string? color) => Formatting.HexColor.IsHex(color);

    private static int Version(JsonObject json) =>
        json[VersionField] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;

    private static ReportDefinition Clean(ReportDefinition definition) {
        var query = definition.Query ?? new ReportQuery("");
        var view = definition.View ?? new ReportView(ReportCharts.Table);
        return definition with {
            Id = definition.Id ?? "",
            Title = definition.Title ?? "",
            Query = query with {
                GroupBy = query.GroupBy ?? Array.Empty<GroupKey>(),
                Measures = query.Measures ?? Array.Empty<MeasureRequest>(),
                Filters = query.Filters ?? FilterSet.Empty,
                Normalize = query.Normalize ?? Normalize.None,
                Sort = query.Sort ?? Array.Empty<SortKey>(),
                Having = query.Having ?? Array.Empty<HavingClause>(),
            },
            View = view with {
                Chart = view.Chart ?? ReportCharts.Table,
                Labels = view.Labels ?? new Dictionary<string, string>(StringComparer.Ordinal),
                Options = view.Options ?? new Dictionary<string, string>(StringComparer.Ordinal),
                Color = IsHex(view.Color) ? view.Color : null,
                Unfilled = IsHex(view.Unfilled) ? view.Unfilled : null,
                LabelColors = (view.LabelColors ?? new Dictionary<string, string>(StringComparer.Ordinal))
                    .Where(kv => IsHex(kv.Value))
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
            },
        };
    }
}
