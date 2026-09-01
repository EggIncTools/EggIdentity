using System.Text.Json.Serialization;

namespace EggIdentity.Styles.Theming;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ThemeTokenValue(
    [property: JsonPropertyName("hex")] [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Hex = null,
    [property: JsonPropertyName("l")] [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? L = null,
    [property: JsonPropertyName("c")] [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? C = null,
    [property: JsonPropertyName("h")] [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? H = null) {
    public ThemeColor? Resolve() {
        if (Hex is not null) return L is null && C is null && H is null ? ThemeColor.FromHex(Hex) : null;
        if (L is { } l && C is { } c && H is { } h) return ThemeColor.FromOklch(l, c, h);
        return null;
    }
}
