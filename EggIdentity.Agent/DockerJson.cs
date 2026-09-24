using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace EggIdentity.Agent;

public sealed record PullProgress(string? Status, string? Id, string? Progress, string? Error) {
    public string Format() {
        if (Error is not null) return "error: " + Error;
        var head = Id is null ? Status ?? "" : $"{Id}: {Status}";
        return Progress is null ? head : $"{head} {Progress}";
    }
}

public static class DockerJson {
    public static ContainerInfo ParseContainer(JsonElement container, JsonElement? image) {
        var id = container.GetProperty("Id").GetString() ?? "";
        var name = (container.TryGetProperty("Name", out var n) ? n.GetString() : null) ?? "";
        var config = container.TryGetProperty("Config", out var c) ? c : EmptyObject();
        var running = container.TryGetProperty("State", out var state)
            && state.TryGetProperty("Running", out var r)
            && r.ValueKind == JsonValueKind.True;
        var imageRef = config.TryGetProperty("Image", out var img) ? img.GetString() ?? "" : "";
        var imageId = container.TryGetProperty("Image", out var iid) ? iid.GetString() ?? "" : "";
        var labels = ReadStringMap(config, "Labels");
        var env = ReadStringList(config, "Env");
        var repoDigests = new List<string>();
        if (image is { } imageElement) {
            repoDigests = ReadStringList(imageElement, "RepoDigests");
            if (imageElement.TryGetProperty("Config", out var ic) && ic.ValueKind == JsonValueKind.Object) {
                foreach (var (k, v) in ReadStringMap(ic, "Labels"))
                    labels.TryAdd(k, v);
            }
        }
        return new ContainerInfo(id, name.TrimStart('/'), imageRef, imageId, repoDigests, env, labels, running);
    }

    public static ImageInfo ParseImage(JsonElement image) {
        var id = image.GetProperty("Id").GetString() ?? "";
        var config = image.TryGetProperty("Config", out var c) ? c : EmptyObject();
        return new ImageInfo(id, ReadStringList(image, "RepoDigests"), ReadStringMap(config, "Labels"), ReadStringList(config, "Env"));
    }

    public static string DemuxLogStream(ReadOnlySpan<byte> data) {
        if (!LooksFramed(data)) return Encoding.UTF8.GetString(data);

        var sb = new StringBuilder(data.Length);
        var offset = 0;
        while (offset + 8 <= data.Length) {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 4, 4));
            offset += 8;
            var take = Math.Min(length, data.Length - offset);
            sb.Append(Encoding.UTF8.GetString(data.Slice(offset, take)));
            offset += take;
        }
        return sb.ToString();
    }

    private static bool LooksFramed(ReadOnlySpan<byte> data) =>
        data.Length >= 8 && data[0] <= 2 && data[1] == 0 && data[2] == 0 && data[3] == 0;

    public static PullProgress? ParsePullProgress(string line) {
        if (string.IsNullOrWhiteSpace(line)) return null;
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        return root.ValueKind != JsonValueKind.Object
            ? null
            : new PullProgress(
                ReadString(root, "status"),
                ReadString(root, "id"),
                ReadString(root, "progress"),
                ReadString(root, "error"));
    }

    public static string? ReadErrorMessage(string body) {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? ReadString(doc.RootElement, "message") : null;
        } catch (JsonException) {
            return body.Trim();
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string> ReadStringList(JsonElement element, string name) {
        var list = new List<string>();
        if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) return list;
        foreach (var item in array.EnumerateArray()) {
            if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString() ?? "");
        }
        return list;
    }

    private static Dictionary<string, string> ReadStringMap(JsonElement element, string name) {
        return !element.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object
            ? []
            : obj.EnumerateObject()
                .Where(property => property.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(property => property.Name, property => property.Value.GetString() ?? "", StringComparer.Ordinal);
    }

    private static JsonElement EmptyObject() {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}
