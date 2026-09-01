using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EggIdentity.Styles.Theming;

public static class ThemeSchema {
    public const string Current = "eggidentity-theme/1";
    public const string LegacyEgiV1 = "egi-theme/1";
    public const int CurrentVersion = 1;

    public static readonly IReadOnlyList<string> Accepted = [Current, LegacyEgiV1];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial record ThemeModel(
    [property: JsonPropertyName("$schema")] string Schema,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("tokens")] IReadOnlyDictionary<string, ThemeTokenValue> Tokens,
    [property: JsonPropertyName("chroma")] ThemeChroma Chroma,
    [property: JsonPropertyName("css")] string Css) {
    public const int MaxNameLength = 64;
    public const int MaxSlugLength = 64;
    public const int MaxCssSourceBytes = 16 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ThemeColor? ResolveToken(string name) =>
        Tokens.TryGetValue(name, out var value) && value.Resolve() is { } color ? color : null;

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static (ThemeModel? Model, IReadOnlyList<string> Errors) Parse(string json, ThemeTokenRegistry registry) {
        var errors = new List<string>();
        ThemeModel? raw;
        try {
            raw = JsonSerializer.Deserialize<ThemeModel>(json, JsonOptions);
        } catch (JsonException ex) {
            return (null, [ex.Message]);
        }

        if (raw is null) return (null, ["empty document"]);
        if (!ThemeSchema.Accepted.Contains(raw.Schema)) errors.Add($"unknown $schema '{raw.Schema}'");
        if (raw.SchemaVersion != ThemeSchema.CurrentVersion)
            errors.Add($"unknown schemaVersion {raw.SchemaVersion}, expected {ThemeSchema.CurrentVersion}");
        if (string.IsNullOrWhiteSpace(raw.Name) || raw.Name.Length > MaxNameLength) errors.Add("name must be 1 to 64 chars");
        if (raw.Slug is null || !SlugPattern().IsMatch(raw.Slug)) errors.Add("slug must match [a-z0-9-]{1,64}");

        var tokens = new Dictionary<string, ThemeTokenValue>(StringComparer.Ordinal);
        foreach (var (key, value) in raw.Tokens ?? new Dictionary<string, ThemeTokenValue>()) {
            if (registry.Canonicalize(key) is not { } canonical) {
                errors.Add($"unknown token '{key}'");
                continue;
            }

            if (value?.Resolve() is null) {
                errors.Add($"token '{key}' must be a hex value or an oklch triple");
                continue;
            }

            tokens[canonical] = value;
        }

        string css = raw.Css ?? "";
        if (Encoding.UTF8.GetByteCount(css) > MaxCssSourceBytes)
            errors.Add($"css source over {MaxCssSourceBytes / 1024} KB");

        if (errors.Count > 0) return (null, errors);
        return (raw with { Tokens = tokens, Chroma = (raw.Chroma ?? ThemeChroma.None).Clamped(), Css = css }, errors);
    }

    [GeneratedRegex("^[a-z0-9-]{1,64}$", RegexOptions.Compiled)]
    private static partial Regex SlugPattern();
}
