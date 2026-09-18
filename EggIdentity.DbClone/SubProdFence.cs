namespace EggIdentity.DbClone;

public sealed record FenceGate(string Name, IReadOnlyList<string> EnvKeys);

public sealed record FenceReportEntry(string Gate, string AllowKey, bool Open, string EnvKey, bool Fenced);

public sealed class SubProdFence(IReadOnlyList<FenceGate> gates) {
    public const string AllowPrefix = "SUBPROD_ALLOW_";

    public IReadOnlyList<FenceGate> Gates => gates;

    public static string AllowKey(string gate) {
        ArgumentException.ThrowIfNullOrWhiteSpace(gate);
        return AllowPrefix + new string([.. gate.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_')]);
    }

    public static bool IsOpen(FenceGate gate, Func<string, string?> environment) {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(environment);
        return string.Equals(environment(AllowKey(gate.Name))?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    public static bool Applies(Func<string, string?> environment) =>
        EnvironmentSettings.IsSubProd(EnvironmentSettings.Resolve(environment));

    public Func<string, string?> Wrap(Func<string, string?> getter) {
        ArgumentNullException.ThrowIfNull(getter);
        if (!Applies(getter)) return getter;
        var fenced = new HashSet<string>(
            gates.Where(g => !IsOpen(g, getter)).SelectMany(g => g.EnvKeys), StringComparer.Ordinal);
        return key => fenced.Contains(key) ? "" : getter(key);
    }

    public IReadOnlyList<FenceReportEntry> Report(Func<string, string?>? environment = null) {
        var env = environment ?? Environment.GetEnvironmentVariable;
        var applies = Applies(env);
        var entries = new List<FenceReportEntry>();
        foreach (var gate in gates) {
            var open = IsOpen(gate, env);
            var allowKey = AllowKey(gate.Name);
            entries.AddRange(gate.EnvKeys.Select(k => new FenceReportEntry(gate.Name, allowKey, open, k, applies && !open)));
        }
        return entries;
    }
}
