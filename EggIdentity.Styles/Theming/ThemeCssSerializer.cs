using System.Buffers;
using System.Globalization;
using System.Text;

namespace EggIdentity.Styles.Theming;

public enum ThemeScope {
    Live,
    Preview
}

public sealed record ThemeCssSerializeResult(string Output, bool Ok, string? Reason);

public static class ThemeCssSerializer {
    public const string LivePrefix = "html[data-eggidentity-theme=\"u\"]";
    public const string PreviewPrefix = ".theme-preview-scope";
    public const int MaxLane2OutputBytes = 8 * 1024;

    private static readonly string[] AllowedAtPrefixes = ["@keyframes", "@media"];
    private static readonly SearchValues<char> Lane2Forbidden = SearchValues.Create("<\\@&");

    public static ThemeCssSerializeResult SerializeLane2(string css, ThemeScope scope, ThemeCssCatalog catalog, ThemeTokenRegistry tokens, int maxSourceBytes) {
        string root = scope == ThemeScope.Live ? LivePrefix : PreviewPrefix;
        var parsed = ThemeCssParser.Parse(css, catalog, tokens, maxSourceBytes);
        if (!parsed.Ok) return new ThemeCssSerializeResult("", false, "parse failed");

        var sb = new StringBuilder();
        foreach (var rule in parsed.Rules) {
            sb.Append(ScopedSelector(root, rule.Entry.Selector)).Append(" {\n");
            foreach (var decl in rule.Declarations) {
                sb.Append("  ").Append(decl.Property).Append(": ");
                for (int g = 0; g < decl.Groups.Count; g++) {
                    if (g > 0) sb.Append(", ");
                    AppendParts(sb, decl.Groups[g]);
                }

                sb.Append(";\n");
            }

            sb.Append("}\n");
        }

        string output = sb.ToString();
        if (Encoding.UTF8.GetByteCount(output) > MaxLane2OutputBytes) return new ThemeCssSerializeResult("", false, "lane-2 output over size cap");
        if (!Lane2AlphabetOk(output)) return new ThemeCssSerializeResult("", false, "lane-2 self-check failed");
        if (!OutputAlphabetOk(output)) return new ThemeCssSerializeResult("", false, "serializer self-check failed");
        return new ThemeCssSerializeResult(output, true, null);
    }

    private static string ScopedSelector(string root, string canonical) {
        var parts = canonical.Split(", ");
        return string.Join(", ", parts.Select(p => $"{root} {p}"));
    }

    private static void AppendParts(StringBuilder sb, IReadOnlyList<CssPart> parts) {
        for (int i = 0; i < parts.Count; i++) {
            if (i > 0) sb.Append(' ');
            AppendPart(sb, parts[i]);
        }
    }

    private static void AppendPart(StringBuilder sb, CssPart part) {
        switch (part) {
            case CssKeyword kw:
                sb.Append(kw.Text);
                break;
            case CssNumber num:
                sb.Append(ThemeCssParser.FormatNumber(num.Value)).Append(num.Unit);
                break;
            case CssHex hex:
                sb.Append('#').Append(hex.R.ToString("x2", CultureInfo.InvariantCulture))
                    .Append(hex.G.ToString("x2", CultureInfo.InvariantCulture))
                    .Append(hex.B.ToString("x2", CultureInfo.InvariantCulture));
                if (hex.A is { } a) sb.Append(a.ToString("x2", CultureInfo.InvariantCulture));
                break;
            case CssFunc fn:
                sb.Append(fn.Name).Append('(');
                for (int i = 0; i < fn.Args.Count; i++) {
                    if (i > 0) sb.Append(", ");
                    AppendParts(sb, fn.Args[i]);
                }

                sb.Append(')');
                break;
        }
    }

    private static bool Lane2AlphabetOk(string output) =>
        !output.AsSpan().ContainsAny(Lane2Forbidden);

    private static bool OutputAlphabetOk(string output) {
        var span = output.AsSpan();
        if (span.ContainsAny('<', '\\', '&')) return false;
        for (int i = 0; i < span.Length; i++) {
            if (span[i] != '@') continue;
            bool allowed = false;
            foreach (string prefix in AllowedAtPrefixes) {
                if (span[i..].StartsWith(prefix, StringComparison.Ordinal)) {
                    allowed = true;
                    break;
                }
            }

            if (!allowed) return false;
        }

        return true;
    }
}
