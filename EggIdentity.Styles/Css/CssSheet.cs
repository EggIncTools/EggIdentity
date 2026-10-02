using System.Text;
using System.Text.RegularExpressions;

namespace EggIdentity.Styles.Css;

public sealed record CssDeclaration(string Property, string Value);

public sealed record CssRule(string Selector, IReadOnlyList<CssDeclaration> Declarations, string? Container) {
    public string? this[string property] => Declarations.LastOrDefault(d => d.Property == property)?.Value;

    public bool Declares(string property) => Declarations.Any(d => d.Property == property);
}

public sealed partial class CssSheet {
    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"var\((--[A-Za-z0-9_-]+)")]
    private static partial Regex VarRead();

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b|\b(?:rgba?|hsla?|oklch|oklab|lab|lch|hwb|color)\([^()]*(?:\([^()]*\)[^()]*)*\)")]
    private static partial Regex ColorLiteral();

    private CssSheet(List<CssRule> rules, List<string> statements) {
        Rules = rules;
        Statements = statements;
        Selectors = rules.Where(r => r.Container is null || !r.Container.StartsWith("@keyframes", StringComparison.Ordinal))
            .Select(r => r.Selector).ToHashSet(StringComparer.Ordinal);
        Containers = rules.Where(r => r.Container is not null).Select(r => r.Container!).ToHashSet(StringComparer.Ordinal);
        DefinedProperties = rules.SelectMany(r => r.Declarations).Where(d => d.Property.StartsWith("--", StringComparison.Ordinal))
            .Select(d => d.Property).ToHashSet(StringComparer.Ordinal);
        ReadProperties = rules.SelectMany(r => r.Declarations).SelectMany(d => VarRead().Matches(d.Value).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        Literals = rules.SelectMany(r => r.Declarations).SelectMany(d => ColorLiteral().Matches(StripVarFallbacks(d.Value)).Select(m => m.Value))
            .ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<CssRule> Rules { get; }

    public IReadOnlyList<string> Statements { get; }

    public IReadOnlySet<string> Selectors { get; }

    public IReadOnlySet<string> Containers { get; }

    public IReadOnlySet<string> DefinedProperties { get; }

    public IReadOnlySet<string> ReadProperties { get; }

    public IReadOnlySet<string> Literals { get; }

    public IEnumerable<CssRule> Find(string selector) => Rules.Where(r => r.Selector == selector);

    public CssRule? Rule(string selector) => Rules.FirstOrDefault(r => r.Selector == selector && r.Container is null);

    public IEnumerable<CssRule> Within(string containerPrefix) =>
        Rules.Where(r => r.Container is not null && r.Container.StartsWith(containerPrefix, StringComparison.Ordinal));

    public static CssSheet Load(string path) => Parse(File.ReadAllText(path));

    public static CssSheet Parse(string css) {
        var rules = new List<CssRule>();
        var statements = new List<string>();
        var text = Comments().Replace(css, "");
        var pos = 0;
        ParseBlock(text, ref pos, null, rules, statements);
        return new CssSheet(rules, statements);
    }

    private static void ParseBlock(string s, ref int pos, string? container, List<CssRule> rules, List<string> statements) {
        while (pos < s.Length) {
            SkipWs(s, ref pos);
            if (pos >= s.Length) return;
            if (s[pos] == '}') {
                pos++;
                return;
            }
            var preludeStart = pos;
            while (pos < s.Length && s[pos] != '{' && s[pos] != ';') pos++;
            if (pos >= s.Length) return;
            var prelude = Collapse(s[preludeStart..pos]);
            if (s[pos] == ';') {
                pos++;
                if (prelude.Length > 0) statements.Add(prelude);
                continue;
            }
            pos++;
            if (BodyNests(s, pos)) {
                ParseBlock(s, ref pos, prelude, rules, statements);
            } else {
                rules.Add(new CssRule(prelude, ParseDeclarations(s, ref pos), container));
            }
        }
    }

    private static bool BodyNests(string s, int pos) {
        var depth = 1;
        for (var i = pos; i < s.Length && depth > 0; i++) {
            if (s[i] == '{') return true;
            if (s[i] == '}') depth--;
        }
        return false;
    }

    private static List<CssDeclaration> ParseDeclarations(string s, ref int pos) {
        var decls = new List<CssDeclaration>();
        while (pos < s.Length) {
            SkipWs(s, ref pos);
            if (pos >= s.Length) break;
            if (s[pos] == '}') {
                pos++;
                break;
            }
            var start = pos;
            var depth = 0;
            while (pos < s.Length) {
                var c = s[pos];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ';' && depth == 0) break;
                else if (c == '}' && depth == 0) break;
                pos++;
            }
            var raw = s[start..pos];
            if (pos < s.Length && s[pos] == ';') pos++;
            var colon = raw.IndexOf(':');
            if (colon <= 0) continue;
            decls.Add(new CssDeclaration(raw[..colon].Trim(), Collapse(raw[(colon + 1)..])));
        }
        return decls;
    }

    private static void SkipWs(string s, ref int pos) {
        while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
    }

    private static string Collapse(string value) {
        var sb = new StringBuilder(value.Length);
        var ws = false;
        foreach (var c in value.Trim()) {
            if (char.IsWhiteSpace(c)) {
                ws = true;
                continue;
            }
            if (ws && sb.Length > 0) sb.Append(' ');
            ws = false;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string StripVarFallbacks(string value) {
        var sb = new StringBuilder(value.Length);
        var i = 0;
        while (i < value.Length) {
            var at = value.IndexOf("var(", i, StringComparison.Ordinal);
            if (at < 0) {
                sb.Append(value, i, value.Length - i);
                break;
            }
            sb.Append(value, i, at - i);
            var depth = 1;
            var j = at + 4;
            var comma = -1;
            while (j < value.Length && depth > 0) {
                var c = value[j];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 1 && comma < 0) comma = j;
                j++;
            }
            sb.Append(value, at, (comma < 0 ? j : comma) - at).Append(')');
            i = j;
        }
        return sb.ToString();
    }
}
