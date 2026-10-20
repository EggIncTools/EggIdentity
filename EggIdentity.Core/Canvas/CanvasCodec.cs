using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EggIdentity.Canvas;

internal sealed class TextAlignConverter : JsonStringEnumConverter<TextAlign> {
    public TextAlignConverter() : base(JsonNamingPolicy.CamelCase) {
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = false,
    Converters = [typeof(TextAlignConverter)])]
[JsonSerializable(typeof(CanvasDoc))]
[JsonSerializable(typeof(CanvasItem))]
[JsonSerializable(typeof(ItemStyle))]
internal sealed partial class CanvasJsonContext : JsonSerializerContext {
}

public static class CanvasCodec {
    public const int CurrentVersion = 1;

    public static JsonSerializerOptions Options => CanvasJsonContext.Default.Options;

    public static string Serialize(CanvasDoc doc) {
        ArgumentNullException.ThrowIfNull(doc);
        return JsonSerializer.Serialize(CanvasOps.Clean(doc with { Version = CurrentVersion }), CanvasJsonContext.Default.CanvasDoc);
    }

    public static CanvasDoc Deserialize(string json) {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var node = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("canvas doc must be an object");
        var version = node["version"]?.GetValue<int>() ?? 1;
        if (version > CurrentVersion) throw new JsonException($"canvas version {version} is newer than {CurrentVersion}");
        var doc = node.Deserialize(CanvasJsonContext.Default.CanvasDoc) ?? throw new JsonException("canvas doc is null");
        return CanvasOps.Clean(doc with { Version = CurrentVersion });
    }
}
