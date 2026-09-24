using System.Text.Json;
using EggIdentity.Contract;

namespace EggIdentity.Bot;

public static class MessageSpecs {
    public static MessageSpec Resolve(string? messageJson, string? embedJson, EmbedSpec defaultEmbed) =>
        ParseMessage(messageJson) ?? MessageSpec.FromEmbed(ParseEmbed(embedJson) ?? defaultEmbed);

    public static MessageSpec? ParseMessage(string? json) {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try {
            return JsonSerializer.Deserialize<MessageSpec>(json);
        } catch (JsonException) {
            return null;
        }
    }

    public static EmbedSpec? ParseEmbed(string? json) {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try {
            return JsonSerializer.Deserialize<EmbedSpec>(json);
        } catch (JsonException) {
            return null;
        }
    }
}
