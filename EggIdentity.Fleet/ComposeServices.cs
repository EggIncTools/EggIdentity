using EggIdentity.Contract;
using YamlDotNet.RepresentationModel;

namespace EggIdentity.Fleet;

public static class ComposeServices {
    public static IReadOnlyList<StackService> List(string composeText) {
        ArgumentNullException.ThrowIfNull(composeText);
        var stream = new YamlStream();
        using var reader = new StringReader(composeText);
        stream.Load(reader);
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root) return [];
        if (!root.Children.TryGetValue(new YamlScalarNode("services"), out var node) || node is not YamlMappingNode services) return [];

        var list = new List<StackService>();
        foreach (var (keyNode, valueNode) in services.Children) {
            var key = (keyNode as YamlScalarNode)?.Value;
            if (string.IsNullOrWhiteSpace(key)) continue;
            var service = valueNode as YamlMappingNode;
            list.Add(new StackService(key, NonEmpty(service, "container_name"), NonEmpty(service, "image")));
        }
        return list;
    }

    private static string? NonEmpty(YamlMappingNode? map, string key) =>
        map is not null && Scalar(map, key) is { Length: > 0 } value ? value.Trim() : null;

    private static string? Scalar(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? (value as YamlScalarNode)?.Value : null;
}
