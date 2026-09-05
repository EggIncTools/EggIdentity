using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace EggIdentity.Agent;

public sealed record ComposeServiceInfo(
    bool Found,
    IReadOnlyList<string> EnvironmentKeys,
    bool HasEnvFile,
    IReadOnlySet<string> ReferencedVariables) {
    public static ComposeServiceInfo Missing(IReadOnlySet<string> referenced) => new(false, [], false, referenced);
}

public static partial class ComposeEnv {
    [GeneratedRegex(@"\$(?:\{(?<braced>[A-Za-z_][A-Za-z0-9_]*)(?:[:?+-][^}]*)?\}|(?<bare>[A-Za-z_][A-Za-z0-9_]*))")]
    private static partial Regex VariableReference();

    public static ComposeServiceInfo Parse(string composeText, string serviceName) {
        ArgumentNullException.ThrowIfNull(composeText);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var referenced = FindReferencedVariables(composeText);
        var service = FindService(composeText, serviceName);
        if (service is null) return ComposeServiceInfo.Missing(referenced);

        var keys = TryGet(service, "environment") is { } env ? ReadEnvironmentKeys(env) : [];
        var hasEnvFile = TryGet(service, "env_file") is not null;
        return new ComposeServiceInfo(true, keys, hasEnvFile, referenced);
    }

    public static IReadOnlySet<string> FindReferencedVariables(string composeText) {
        ArgumentNullException.ThrowIfNull(composeText);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var unescaped = composeText.Replace("$$", "\0", StringComparison.Ordinal);
        foreach (Match m in VariableReference().Matches(unescaped)) {
            var name = m.Groups["braced"].Success ? m.Groups["braced"].Value : m.Groups["bare"].Value;
            names.Add(name);
        }
        return names;
    }

    private static YamlMappingNode? FindService(string composeText, string serviceName) {
        var stream = new YamlStream();
        using var reader = new StringReader(composeText);
        stream.Load(reader);
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root) return null;
        if (TryGet(root, "services") is not YamlMappingNode services) return null;

        YamlMappingNode? byContainerName = null;
        foreach (var (keyNode, valueNode) in services.Children) {
            if (valueNode is not YamlMappingNode service) continue;
            var key = (keyNode as YamlScalarNode)?.Value;
            if (string.Equals(key, serviceName, StringComparison.Ordinal)) return service;
            var containerName = (TryGet(service, "container_name") as YamlScalarNode)?.Value;
            if (byContainerName is null && string.Equals(containerName, serviceName, StringComparison.Ordinal))
                byContainerName = service;
        }
        return byContainerName;
    }

    private static List<string> ReadEnvironmentKeys(YamlNode environment) {
        var keys = new List<string>();
        switch (environment) {
            case YamlSequenceNode list:
                foreach (var item in list.Children) {
                    var text = (item as YamlScalarNode)?.Value;
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var eq = text.IndexOf('=', StringComparison.Ordinal);
                    keys.Add((eq < 0 ? text : text[..eq]).Trim());
                }
                break;
            case YamlMappingNode map:
                foreach (var (keyNode, _) in map.Children) {
                    var key = (keyNode as YamlScalarNode)?.Value;
                    if (!string.IsNullOrWhiteSpace(key)) keys.Add(key.Trim());
                }
                break;
        }
        return keys;
    }

    private static YamlNode? TryGet(YamlMappingNode map, string key) =>
        map.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;
}
