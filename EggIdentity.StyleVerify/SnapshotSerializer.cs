using System.Text.Json;

namespace EggIdentity.StyleVerify;

public static class SnapshotSerializer {
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string ToJson(PageSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);

    public static PageSnapshot FromJson(string json) =>
        JsonSerializer.Deserialize<PageSnapshot>(json, Options)
        ?? throw new JsonException("Deserialized PageSnapshot was null.");
}
