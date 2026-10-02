using System.Text.RegularExpressions;
using EggIdentity.Styles.Css;

namespace EggIdentity.Styles.Tests;

public partial class SharedSheetTests {
    [GeneratedRegex(@"^rgba\(0,0,0,\.\d+\)$|^#0000$")]
    private static partial Regex NeutralLiteral();

    private static readonly CssSheet Sheet = CssSheet.Load(Path.Combine(RepoRoot(), "EggIdentity.Styles", "wwwroot", "shared.css"));
    private static readonly CssSheet Preflight = CssSheet.Load(Path.Combine(RepoRoot(), "EggIdentity.Styles", "wwwroot", "preflight.css"));

    private static string RepoRoot() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EggIdentity.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("EggIdentity.slnx not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void EveryDictionarySelectorIsEmittedWithDeclarations() {
        foreach (var selector in ComponentClasses.All.Keys) {
            var rule = Sheet.Rule(selector);
            Assert.True(rule is { Declarations.Count: > 0 }, selector);
        }
    }

    [Fact]
    public void NoPaletteIsDefined() {
        Assert.DoesNotContain(Sheet.DefinedProperties, p => p.StartsWith("--color-", StringComparison.Ordinal));
        Assert.DoesNotContain(Preflight.DefinedProperties, p => p.StartsWith("--color-", StringComparison.Ordinal));
        Assert.DoesNotContain(Preflight.ReadProperties, p => p.StartsWith("--color-", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryPaletteReadIsAContractToken() {
        var contract = ComponentTokens.Required.Concat(ComponentTokens.Optional).ToHashSet(StringComparer.Ordinal);
        var reads = Sheet.ReadProperties.Where(p => p.StartsWith("--color-", StringComparison.Ordinal));
        Assert.All(reads, p => Assert.Contains(p, contract));
    }

    [Fact]
    public void LiteralColorsAreOnlyNeutralAlphas() {
        Assert.All(Sheet.Literals, l => Assert.Matches(NeutralLiteral(), l));
    }

    [Fact]
    public void NoCascadeLayers() {
        Assert.DoesNotContain(Sheet.Statements, s => s.StartsWith("@layer", StringComparison.Ordinal));
        Assert.DoesNotContain(Sheet.Containers, c => c.StartsWith("@layer", StringComparison.Ordinal));
        Assert.DoesNotContain(Preflight.Containers, c => c.StartsWith("@layer", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryMotionRuleHasAReducedMotionCounterpart() {
        var moving = Sheet.Rules
            .Where(r => r.Container is null && r.Declarations.Any(d => d.Property is "transition" or "animation" && d.Value != "none"))
            .Select(r => r.Selector);
        var calmed = Sheet.Within("@media (prefers-reduced-motion").SelectMany(r => r.Selector.Split(", ")).ToHashSet(StringComparer.Ordinal);
        Assert.All(moving, s => Assert.Contains(s, calmed));
    }

    [Fact]
    public void EveryMotionRuleReadsTheMotionTokens() {
        var timed = Sheet.Rules
            .Where(r => r.Container is null)
            .SelectMany(r => r.Declarations)
            .Where(d => d.Property is "transition" or "animation" && d.Value != "none")
            .Select(d => StripVars(d.Value));
        Assert.All(timed, v => Assert.DoesNotMatch(LiteralDuration(), v));
    }

    [GeneratedRegex(@"var\([^()]*\)")]
    private static partial Regex InnerVar();

    [GeneratedRegex(@"(?<![\w.])(?!0s\b)\d*\.?\d+m?s\b")]
    private static partial Regex LiteralDuration();

    private static string StripVars(string value) {
        while (InnerVar().IsMatch(value)) value = InnerVar().Replace(value, "");
        return value;
    }

    [Theory]
    [InlineData(".modal-card.wb-card", "width", "var(--wb-card-w,92vw)")]
    [InlineData(".modal-card.wb-card", "height", "var(--wb-card-h,88vh)")]
    [InlineData(".modal-card.wb-card", "max-width", "var(--wb-card-max,80rem)")]
    [InlineData(".wb-rail", "width", "var(--wb-rail-w,18rem)")]
    public void ConsumerPinnedDeclarations(string selector, string property, string value) {
        Assert.Equal(value, Sheet.Rule(selector)?[property]);
    }

    [Fact]
    public void Tokens_MatchGoldenNames() {
        string[] expectedRequired = [
            "--color-bg", "--color-panel", "--color-panel2", "--color-fg", "--color-muted",
            "--color-accent", "--color-accent2", "--color-ok", "--color-err", "--color-border", "--color-warn",
        ];
        Assert.Equal<string>(expectedRequired, [.. ComponentTokens.Required]);
        Assert.Equal<string>(["--color-panel0"], [.. ComponentTokens.Optional]);
    }
}
