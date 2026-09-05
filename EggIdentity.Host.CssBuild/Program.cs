using System.Text;
using EggIdentity.Host.CssBuild;
using EggIdentity.Styles;
using EggIdentity.Styles.Theming;
using MonorailCss;
using MonorailCss.Parser.SourceCss;

if (args.Length < 1) {
    Console.Error.WriteLine("Usage: EggIdentity.Host.CssBuild <EggIdentity.Host project directory>");
    return 1;
}

var hostProjectDir = Path.GetFullPath(args[0]);
if (!Directory.Exists(hostProjectDir)) {
    Console.Error.WriteLine($"Host project directory not found: {hostProjectDir}");
    return 1;
}

var cssSourcePath = Path.Combine(hostProjectDir, "Styles", "app.css");
if (!File.Exists(cssSourcePath)) {
    Console.Error.WriteLine($"CSS source file not found: {cssSourcePath}");
    return 1;
}

var outputPath = Path.Combine(hostProjectDir, "wwwroot", "styles.css");
var repoRoot = Path.GetFullPath(Path.Combine(hostProjectDir, ".."));
var contentFiles = ContentSources.Enumerate(repoRoot).ToList();

var rawSourceText = File.ReadAllText(cssSourcePath);
var applyGuardViolation = CssBuildText.FindSemicolonInsideApplyBracket(rawSourceText);
if (applyGuardViolation is { } violation) {
    Console.Error.WriteLine($"CSS build guard failed: {cssSourcePath}:{violation.Line} has a ';' inside a bracket value within an @apply body, near: {violation.Snippet}");
    Console.Error.WriteLine("Move that ';' outside the bracket value and rebuild.");
    return 1;
}

var registry = HostPalette.BuildRegistry();
foreach (var (tokenName, _) in HostPalette.ComponentColors.Concat(HostPalette.AppColors)) {
    if (!registry.IsKnown(tokenName) || registry.Canonicalize(tokenName) != tokenName) {
        Console.Error.WriteLine($"Palette token '{tokenName}' failed the theme token registry round-trip.");
        return 1;
    }
}

var themeHeaderIndex = rawSourceText.IndexOf("@theme {", StringComparison.Ordinal);
if (themeHeaderIndex < 0) {
    Console.Error.WriteLine($"CSS source has no @theme block: {cssSourcePath}");
    return 1;
}
var themeBraceIndex = rawSourceText.IndexOf('{', themeHeaderIndex);
var themeCloseIndex = FindMatchingBrace(rawSourceText, themeBraceIndex);
var themeBody = rawSourceText.Substring(themeBraceIndex + 1, themeCloseIndex - themeBraceIndex - 1);
if (themeBody.Contains("--color-", StringComparison.Ordinal)) {
    Console.Error.WriteLine($"CSS build drift guard failed: {cssSourcePath} still defines --color- tokens in its @theme block.");
    Console.Error.WriteLine("Color tokens come from EggIdentity.Fallback.FallbackDefaults via EggIdentity.Host.CssBuild/HostPalette.cs; remove them from the CSS file.");
    return 1;
}

var contrastColors = new Dictionary<string, ThemeColor>();
foreach (var contrastName in HostPalette.ContrastBaseTokens.Concat(HostPalette.StatusTokens)) {
    var contrastValue = HostPalette.ComponentColors.First(c => c.Name == contrastName).Value;
    if (ThemeColor.FromHex(contrastValue) is not { } themeColor) {
        Console.Error.WriteLine($"Palette token '{contrastName}' value '{contrastValue}' is not parseable hex for contrast validation.");
        return 1;
    }
    contrastColors[contrastName] = themeColor;
}
var contrastResult = ThemeContrast.Validate(contrastColors, ThemeChroma.None, HostPalette.StatusTokens);
if (!contrastResult.Passes) {
    foreach (var contrastFailure in contrastResult.Failures) {
        Console.WriteLine($"[contrast warning] {contrastFailure.Check}: {contrastFailure.A} vs {contrastFailure.B}, measured {contrastFailure.Measured:0.###}, required {contrastFailure.Required:0.###}");
    }
}

var newline = rawSourceText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
var colorDeclarations = new StringBuilder();
foreach (var (colorName, colorValue) in HostPalette.ComponentColors.Concat(HostPalette.AppColors)) {
    colorDeclarations.Append("  --color-").Append(colorName).Append(": ").Append(colorValue).Append(';').Append(newline);
}
var splicedText = rawSourceText
    .Remove(themeHeaderIndex, "@theme {".Length)
    .Insert(themeHeaderIndex, "@theme {" + newline + colorDeclarations);

Console.WriteLine($"Scanning {contentFiles.Count} content files for utility/component class tokens...");
var candidates = CssBuildText.Scan(contentFiles);
candidates.UnionWith(ContentSafelist.Tokens);
Console.WriteLine($"Found {candidates.Count} distinct candidate tokens.");

var processor = new CssSourceProcessor(message => Console.WriteLine($"[monorail] {message}"));
var sourceResult = processor.ProcessSource(splicedText, cssSourcePath, null);

var mergedApplies = ComponentClasses.All.SetItems(sourceResult.Settings.Applies);
var settings = sourceResult.Settings with { Applies = mergedApplies };

var framework = new CssFramework(settings);
var compiledCss = framework.Process(candidates);

var strippedRawCss = CssBuildText.StripApplyDirectives(sourceResult.RawCss);
var finalCss = CssBuildText.UnwrapLayersAndSpliceRaw(compiledCss, UnwrapLayerWrappers(strippedRawCss));

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, finalCss);

Console.WriteLine($"Wrote {finalCss.Length} chars to {outputPath}");
return 0;

static string UnwrapLayerWrappers(string css) {
    var result = new StringBuilder();
    var i = 0;
    while (true) {
        var atIndex = css.IndexOf("@layer", i, StringComparison.Ordinal);
        if (atIndex < 0) {
            result.Append(css, i, css.Length - i);
            return result.ToString();
        }
        result.Append(css, i, atIndex - i);
        var braceIndex = css.IndexOf('{', atIndex);
        if (braceIndex < 0) {
            result.Append(css, atIndex, css.Length - atIndex);
            return result.ToString();
        }
        var closeIndex = FindMatchingBrace(css, braceIndex);
        result.Append(css, braceIndex + 1, closeIndex - braceIndex - 1);
        i = closeIndex + 1;
    }
}

static int FindMatchingBrace(string text, int openBraceIndex) {
    var depth = 0;
    for (var idx = openBraceIndex; idx < text.Length; idx++) {
        if (text[idx] == '{') {
            depth++;
        } else if (text[idx] == '}') {
            depth--;
            if (depth == 0) {
                return idx;
            }
        }
    }
    throw new InvalidOperationException("Unbalanced braces in CSS while unwrapping @layer.");
}
