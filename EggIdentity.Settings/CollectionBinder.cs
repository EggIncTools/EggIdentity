using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EggIdentity.Settings;

public static class CollectionBinder {
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new StringBoolConverter() },
    };

    public static T Bind<T>(IReadOnlyDictionary<string, string?> values) {
        ArgumentNullException.ThrowIfNull(values);

        var obj = new JsonObject();
        foreach (var (name, value) in values) {
            if (string.IsNullOrEmpty(value)) continue;
            obj[name] = JsonValue.Create(value);
        }
        return obj.Deserialize<T>(Options)
            ?? throw new InvalidOperationException($"row could not be bound to {typeof(T).Name}");
    }

    private sealed class StringBoolConverter : JsonConverter<bool> {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            switch (reader.TokenType) {
                case JsonTokenType.True: return true;
                case JsonTokenType.False: return false;
                case JsonTokenType.String:
                    var s = reader.GetString();
                    if (bool.TryParse(s, out var parsed)) return parsed;
                    if (string.Equals(s, "1", StringComparison.Ordinal)) return true;
                    if (string.Equals(s, "0", StringComparison.Ordinal)) return false;
                    break;
            }
            throw new JsonException("expected true or false");
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
            writer.WriteBooleanValue(value);
    }
}
