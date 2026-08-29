namespace EggIdentity.Agent;

public sealed class AgentRegistry {
    public IReadOnlyDictionary<string, AgentConfig> Apps { get; }

    public static AgentRegistry LoadFromDir(string dir) {
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"agent config dir not found: {dir}");

        var apps = new Dictionary<string, AgentConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(dir, "*.yaml").OrderBy(p => p, StringComparer.Ordinal)) {
            AgentConfig cfg;
            try { cfg = AgentConfig.Parse(File.ReadAllText(path)); } catch (Exception e) { throw new FormatException($"{Path.GetFileName(path)}: {e.Message}", e); }

            if (string.IsNullOrWhiteSpace(cfg.Name))
                throw new FormatException($"{Path.GetFileName(path)}: name is required");
            if (apps.ContainsKey(cfg.Name))
                throw new FormatException($"duplicate app name \"{cfg.Name}\" ({Path.GetFileName(path)})");
            apps[cfg.Name] = cfg;
        }

        if (apps.Count == 0) throw new FormatException($"no *.yaml app configs found in {dir}");
        return new AgentRegistry(apps);
    }

    private AgentRegistry(Dictionary<string, AgentConfig> apps) => Apps = apps;
}
