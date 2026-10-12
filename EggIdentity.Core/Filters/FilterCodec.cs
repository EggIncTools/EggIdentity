using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EggIdentity.Filters;

public static class FilterCodec {
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new ConditionConverter());
        options.Converters.Add(new SetConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    public static string Serialize(FilterSet set) {
        ArgumentNullException.ThrowIfNull(set);
        return JsonSerializer.Serialize(set, Options);
    }

    public static FilterSet Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json) ? FilterSet.Empty : JsonSerializer.Deserialize<FilterSet>(json, Options) ?? FilterSet.Empty;

    public static string SerializeCatalog(FilterCatalog catalog) {
        ArgumentNullException.ThrowIfNull(catalog);
        return JsonSerializer.Serialize(catalog, Options);
    }

    public static FilterCatalog? DeserializeCatalog(string json) => JsonSerializer.Deserialize<FilterCatalog>(json, Options);

    public static string Hash(string scope, FilterSet set) {
        ArgumentNullException.ThrowIfNull(set);
        var sb = new StringBuilder(scope);
        sb.Append('#');
        for (var i = 0; i < set.And.Count; i++) {
            Append(sb, set.And[i]);
            if (i < set.Or.Count) {
                foreach (var sibling in set.Or[i]) {
                    sb.Append('|');
                    Append(sb, sibling);
                }
            }
            sb.Append(';');
        }
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, FilterCondition c) =>
        sb.Append(c.Field).Append('~').Append(c.Op).Append('~').Append(c.Value);

    private sealed class ConditionConverter : JsonConverter<FilterCondition> {
        public override FilterCondition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("filter condition must be an object");
            string field = "", op = "", value = "";
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
                var name = reader.GetString();
                reader.Read();
                var text = reader.TokenType switch {
                    JsonTokenType.String => reader.GetString() ?? "",
                    JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False => Encoding.UTF8.GetString(reader.ValueSpan),
                    JsonTokenType.Null => "",
                    _ => Skip(ref reader),
                };
                if (name == "topLevel") field = text;
                else if (name == "op") op = text;
                else if (name == "val") value = text;
            }
            return new FilterCondition(field, op, value);
        }

        private static string Skip(ref Utf8JsonReader reader) {
            reader.Skip();
            return "";
        }

        public override void Write(Utf8JsonWriter writer, FilterCondition value, JsonSerializerOptions options) {
            writer.WriteStartObject();
            writer.WriteString("topLevel", value.Field);
            writer.WriteString("op", value.Op);
            writer.WriteString("val", value.Value);
            writer.WriteEndObject();
        }
    }

    private sealed class SetConverter : JsonConverter<FilterSet> {
        public override FilterSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            if (reader.TokenType == JsonTokenType.Null) return FilterSet.Empty;
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("filter set must be an object");
            IReadOnlyList<FilterCondition> and = [];
            IReadOnlyList<IReadOnlyList<FilterCondition>> or = [];
            var condition = (JsonConverter<FilterCondition>)options.GetConverter(typeof(FilterCondition));
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) {
                var name = reader.GetString();
                reader.Read();
                if (name == "and") and = Conditions(ref reader, condition, options);
                else if (name == "or") or = Groups(ref reader, condition, options);
                else reader.Skip();
            }
            return new FilterSet(and, or);
        }

        private static List<FilterCondition> Conditions(ref Utf8JsonReader reader, JsonConverter<FilterCondition> condition, JsonSerializerOptions options) {
            var list = new List<FilterCondition>();
            if (reader.TokenType != JsonTokenType.StartArray) {
                reader.Skip();
                return list;
            }
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                if (reader.TokenType != JsonTokenType.Null && condition.Read(ref reader, typeof(FilterCondition), options) is { } c) list.Add(c);
            return list;
        }

        private static List<IReadOnlyList<FilterCondition>> Groups(ref Utf8JsonReader reader, JsonConverter<FilterCondition> condition, JsonSerializerOptions options) {
            var groups = new List<IReadOnlyList<FilterCondition>>();
            if (reader.TokenType != JsonTokenType.StartArray) {
                reader.Skip();
                return groups;
            }
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                groups.Add(reader.TokenType == JsonTokenType.Null ? [] : Conditions(ref reader, condition, options));
            return groups;
        }

        public override void Write(Utf8JsonWriter writer, FilterSet value, JsonSerializerOptions options) {
            var condition = (JsonConverter<FilterCondition>)options.GetConverter(typeof(FilterCondition));
            writer.WriteStartObject();
            writer.WriteStartArray("and");
            foreach (var c in value.And) condition.Write(writer, c, options);
            writer.WriteEndArray();
            writer.WriteStartArray("or");
            for (var row = 0; row < Math.Max(value.And.Count, value.Or.Count); row++) {
                writer.WriteStartArray();
                if (row < value.Or.Count)
                    foreach (var c in value.Or[row]) condition.Write(writer, c, options);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }
}
