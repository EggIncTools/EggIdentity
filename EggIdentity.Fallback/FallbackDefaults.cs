namespace EggIdentity.Fallback;

public static class FallbackDefaults {
    public static readonly IReadOnlyDictionary<string, string> Tokens = new Dictionary<string, string> {
        ["--color-bg"] = "#0b0d12",
        ["--color-panel"] = "#161a24",
        ["--color-panel2"] = "#1a1f2c",
        ["--color-fg"] = "#f3f5f9",
        ["--color-muted"] = "#8992a4",
        ["--color-accent"] = "#7aa2ff",
        ["--color-accent2"] = "#5865f2",
        ["--color-ok"] = "#3fb950",
        ["--color-err"] = "#f85149",
        ["--color-border"] = "#262c3a",
    };
}
