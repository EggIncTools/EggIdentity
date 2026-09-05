using EggIdentity.Settings;

namespace EggIdentity.Agent;

public static class EnvProvenance {
    public static IReadOnlyList<EnvKeyInfo> Build(
        ComposeServiceInfo? compose,
        IReadOnlyList<string> containerEnv,
        IReadOnlyList<string> imageEnv,
        IReadOnlyList<StackEnvEntry> stackVariables) {
        ArgumentNullException.ThrowIfNull(containerEnv);
        ArgumentNullException.ThrowIfNull(imageEnv);
        ArgumentNullException.ThrowIfNull(stackVariables);

        var container = SplitPairs(containerEnv);
        var image = SplitPairs(imageEnv);
        var composeKeys = compose is { Found: true }
            ? new HashSet<string>(compose.EnvironmentKeys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        var entries = new Dictionary<string, EnvKeyInfo>(StringComparer.Ordinal);
        foreach (var (name, value) in container)
            entries[name] = Describe(name, value, Classify(name, compose, composeKeys, image));
        foreach (var name in composeKeys.Where(k => !entries.ContainsKey(k)))
            entries[name] = Describe(name, null, EnvOrigin.ServiceEnvironment);

        var result = entries.Values.ToList();
        foreach (var variable in stackVariables) {
            var referenced = compose is null || compose.ReferencedVariables.Contains(variable.Name);
            result.Add(Describe(variable.Name, variable.Value, EnvOrigin.StackVariable) with { Referenced = referenced });
        }
        return [.. result.OrderBy(e => e.Name, StringComparer.Ordinal).ThenBy(e => e.Origin)];
    }

    private static EnvOrigin Classify(
        string name, ComposeServiceInfo? compose, HashSet<string> composeKeys, Dictionary<string, string> image) {
        if (image.ContainsKey(name)) return EnvOrigin.Image;
        if (compose is not { Found: true }) return EnvOrigin.ServiceEnvironment;
        if (composeKeys.Contains(name)) return EnvOrigin.ServiceEnvironment;
        return compose.HasEnvFile ? EnvOrigin.EnvFile : EnvOrigin.Runtime;
    }

    private static EnvKeyInfo Describe(string name, string? value, EnvOrigin origin) {
        var masked = SecretMasking.LooksSecret(name);
        return new EnvKeyInfo(name, origin) {
            Masked = masked,
            Value = value is null ? null : SecretMasking.Mask(name, value),
        };
    }

    private static Dictionary<string, string> SplitPairs(IReadOnlyList<string> pairs) {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in pairs) {
            if (string.IsNullOrEmpty(pair)) continue;
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0) map[pair] = "";
            else map[pair[..eq]] = pair[(eq + 1)..];
        }
        return map;
    }
}
