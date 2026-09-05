using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EggIdentity.Agent;

public sealed record PullProgress(string? Status, string? Id, string? Progress, string? Error) {
    public string Format() {
        if (Error is not null) return "error: " + Error;
        var head = Id is null ? Status ?? "" : $"{Id}: {Status}";
        return Progress is null ? head : $"{head} {Progress}";
    }
}

public static class DockerJson {
    private static readonly string[] EndpointInputKeys = ["IPAMConfig", "Links", "Aliases", "NetworkID", "DriverOpts"];

    public static ContainerInfo ParseContainer(JsonElement container, JsonElement? image) {
        var id = container.GetProperty("Id").GetString() ?? "";
        var name = (container.TryGetProperty("Name", out var n) ? n.GetString() : null) ?? "";
        var config = container.TryGetProperty("Config", out var c) ? c.Clone() : EmptyObject();
        var hostConfig = container.TryGetProperty("HostConfig", out var h) ? h.Clone() : EmptyObject();
        var networks = container.TryGetProperty("NetworkSettings", out var ns) && ns.TryGetProperty("Networks", out var nw)
            ? nw.Clone()
            : EmptyObject();
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
            if (imageElement.TryGetProperty("Config", out var imageConfig)) {
                foreach (var (k, v) in ReadStringMap(imageConfig, "Labels"))
                    labels.TryAdd(k, v);
            }
        }
        return new ContainerInfo(id, name.TrimStart('/'), imageRef, imageId, repoDigests, env, labels, running, config, hostConfig, networks);
    }

    public static ImageInfo ParseImage(JsonElement image) {
        var id = image.GetProperty("Id").GetString() ?? "";
        var config = image.TryGetProperty("Config", out var c) ? c : EmptyObject();
        return new ImageInfo(id, ReadStringList(image, "RepoDigests"), ReadStringMap(config, "Labels"), ReadStringList(config, "Env"));
    }

    public static string BuildCreateBody(ContainerSpec spec) {
        ArgumentNullException.ThrowIfNull(spec);

        var body = ObjectNode(spec.Config);
        body["Image"] = spec.Image;
        body.Remove("MacAddress");
        if (IsAutoContainerId(body["Hostname"]?.GetValue<string>())) body.Remove("Hostname");
        if (spec.Cmd is not null) body["Cmd"] = StringArray(spec.Cmd);

        var host = ObjectNode(spec.HostConfig);
        if (spec.Binds is not null) host["Binds"] = StringArray(spec.Binds);
        if (spec.AutoRemove) host["AutoRemove"] = true;
        if (spec.NetworkMode is not null) host["NetworkMode"] = spec.NetworkMode;
        body["HostConfig"] = host;

        var endpoints = new JsonObject();
        if (spec.Networks.ValueKind == JsonValueKind.Object) {
            foreach (var network in spec.Networks.EnumerateObject())
                endpoints[network.Name] = ShapeEndpoint(network.Value);
        }
        body["NetworkingConfig"] = new JsonObject { ["EndpointsConfig"] = endpoints };

        return body.ToJsonString();
    }

    public static JsonElement ToElement(JsonNode node) {
        ArgumentNullException.ThrowIfNull(node);
        using var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }

    private static JsonObject ObjectNode(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? JsonNode.Parse(element.GetRawText())!.AsObject() : [];

    private static JsonArray StringArray(IEnumerable<string> values) {
        var array = new JsonArray();
        foreach (var value in values) array.Add(value);
        return array;
    }

    public static bool IsAutoContainerId(string? text) =>
        text is { Length: 12 } && text.All(ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static JsonObject ShapeEndpoint(JsonElement endpoint) {
        var shaped = new JsonObject();
        if (endpoint.ValueKind != JsonValueKind.Object) return shaped;
        foreach (var key in EndpointInputKeys) {
            if (!endpoint.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null) continue;
            if (key == "Aliases") {
                var aliases = new JsonArray();
                foreach (var alias in value.EnumerateArray()) {
                    var text = alias.GetString();
                    if (text is not null && !IsAutoContainerId(text)) aliases.Add(text);
                }
                shaped[key] = aliases;
                continue;
            }
            shaped[key] = JsonNode.Parse(value.GetRawText());
        }
        return shaped;
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
        if (root.ValueKind != JsonValueKind.Object) return null;
        return new PullProgress(
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
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!element.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object) return map;
        foreach (var property in obj.EnumerateObject()) {
            if (property.Value.ValueKind == JsonValueKind.String) map[property.Name] = property.Value.GetString() ?? "";
        }
        return map;
    }

    private static JsonElement EmptyObject() {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}
