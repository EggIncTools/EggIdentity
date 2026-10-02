using System.Text.RegularExpressions;

namespace EggIdentity.Styles.Tests;

public partial class SharedAssetTests {
    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|rgba?\([^)]*\)|hsla?\([^)]*\)|oklch\([^)]*\)|oklab\([^)]*\)")]
    private static partial Regex LiteralColor();

    [GeneratedRegex(@"--color-[a-z0-9-]+\s*:")]
    private static partial Regex PaletteDefinition();

    [GeneratedRegex(@"var\(--[a-z0-9-]+,")]
    private static partial Regex VarWithFallback();

    [GeneratedRegex(@"^rgba\(0,0,0,\.\d+\)$|^#0000$")]
    private static partial Regex NeutralLiteral();

    private static string RepoRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EggIdentity.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir.FullName;
    }

    private static string Shared() => File.ReadAllText(Path.Combine(RepoRoot(), "EggIdentity.Styles", "wwwroot", "shared.css"));

    private static string Preflight() => File.ReadAllText(Path.Combine(RepoRoot(), "EggIdentity.Styles", "wwwroot", "preflight.css"));

    private static string StripVarFallbacks(string css) {
        var sb = new System.Text.StringBuilder(css.Length);
        var i = 0;
        while (i < css.Length) {
            var m = VarWithFallback().Match(css, i);
            if (!m.Success) {
                sb.Append(css, i, css.Length - i);
                break;
            }
            sb.Append(css, i, m.Index + m.Length - i);
            var depth = 1;
            var pos = m.Index + m.Length;
            while (pos < css.Length && depth > 0) {
                if (css[pos] == '(') depth++;
                else if (css[pos] == ')') depth--;
                pos++;
            }
            sb.Append(')');
            i = pos;
        }
        return sb.ToString();
    }

    [Fact]
    public void Shared_DefinesNoPaletteToken() {
        Assert.DoesNotMatch(PaletteDefinition(), Shared());
    }

    [Fact]
    public void Shared_LiteralColorsOnlyInFallbacksShadowsOrEngineProbes() {
        var css = string.Join('\n', Shared().Split('\n').Where(l => !l.Contains("@supports", StringComparison.Ordinal)));
        var offenders = LiteralColor().Matches(StripVarFallbacks(css))
            .Select(m => m.Value)
            .Where(v => !NeutralLiteral().IsMatch(v))
            .Distinct()
            .ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Shared_ContainsEverySharedSelector() {
        var css = Shared();
        foreach (var selector in ComponentClasses.All.Keys) {
            Assert.Contains(selector + " {", css);
        }
    }

    [Fact]
    public void Shared_HasNoLayerBlocks() {
        Assert.DoesNotContain("@layer", Shared());
        Assert.DoesNotContain("@layer", Preflight());
    }

    [Fact]
    public void Preflight_ResetsBoxModelAndReadsNoPalette() {
        var css = Preflight();
        Assert.Contains("box-sizing: border-box", css);
        Assert.DoesNotContain("--color-", css);
    }
}
