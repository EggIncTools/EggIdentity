namespace EggIdentity.Fallback;

public static class FallbackDefaults {
    public static readonly IReadOnlyDictionary<string, string> Tokens = new Dictionary<string, string> {
        ["--color-bg"] = "#0f0d09",
        ["--color-panel0"] = "#15130f",
        ["--color-panel"] = "#1c1a16",
        ["--color-panel2"] = "#221f1b",
        ["--color-fg"] = "#f7f5f1",
        ["--color-muted"] = "#9a9083",
        ["--color-accent"] = "#f5b93b",
        ["--color-accent2"] = "#f0b232",
        ["--color-ok"] = "#4ea55a",
        ["--color-err"] = "#e5484d",
        ["--color-border"] = "#322b21",
    };
}
