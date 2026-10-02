using System.Text;
using EggIdentity.Styles;
using MonorailCss;
using MonorailCss.Theme;

namespace EggIdentity.Tools;

internal static class SharedStylesheet {
    internal const string SharedFile = "shared.css";
    internal const string PreflightFile = "preflight.css";
    private const string Placeholder = "#000000";

    internal static int Run(string[] args) {
        if (args.Length == 0 || args[0] != "emit") {
            Console.Error.WriteLine("usage: css emit [output-dir]");
            return 1;
        }
        var dir = args.Length > 1 ? args[1] : Path.Combine(RepoRoot(), "EggIdentity.Styles", "wwwroot");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, SharedFile), RenderShared());
        File.WriteAllText(Path.Combine(dir, PreflightFile), RenderPreflight());
        Console.WriteLine($"css: wrote {SharedFile} and {PreflightFile} to {dir}");
        return 0;
    }

    internal static string RenderShared() {
        var framework = new CssFramework(new CssFrameworkSettings {
            Theme = PlaceholderTheme(),
            IncludePreflight = false,
            Applies = ComponentClasses.All,
        });
        var compiled = framework.Process("");
        return Normalize(StripPalette(CssLayers.Unwrap(compiled)) + "\n" + SharedLiterals.Tail + "\n");
    }

    internal static string RenderPreflight() {
        var framework = new CssFramework(new CssFrameworkSettings {
            Theme = Theme.CreateWithDefaults([]),
            IncludePreflight = true,
        });
        var compiled = framework.Process("flex");
        return Normalize(CssLayers.ExtractLayer(compiled, "base"));
    }

    private static Theme PlaceholderTheme() {
        var theme = Theme.CreateWithDefaults([]);
        foreach (var token in ComponentTokens.Required.Concat(ComponentTokens.Optional)) theme = theme.Add(token, Placeholder);
        return theme;
    }

    private static string StripPalette(string css) {
        var sb = new StringBuilder(css.Length);
        foreach (var line in css.Split('\n')) {
            if (line.TrimStart().StartsWith("--color-", StringComparison.Ordinal)) continue;
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    private static string Normalize(string css) {
        var sb = new StringBuilder(css.Length);
        var blank = false;
        foreach (var raw in css.Replace("\r\n", "\n").Split('\n')) {
            var line = raw.TrimEnd();
            if (line.Length == 0) {
                if (!blank && sb.Length > 0) sb.Append('\n');
                blank = true;
                continue;
            }
            blank = false;
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    internal static string RepoRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EggIdentity.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("css: EggIdentity.slnx not found above " + AppContext.BaseDirectory);
    }
}
