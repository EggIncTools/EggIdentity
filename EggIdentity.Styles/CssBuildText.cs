using System.Text;
using System.Text.RegularExpressions;

namespace EggIdentity.Styles;

public static partial class CssBuildText {
    [GeneratedRegex(@"!?-?[a-zA-Z0-9_][a-zA-Z0-9_:/.\[\]%!-]*")]
    private static partial Regex TokenPattern();

    public static HashSet<string> Scan(IEnumerable<string> filePaths) {
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in filePaths) {
            var text = File.ReadAllText(path);
            foreach (Match match in TokenPattern().Matches(text)) {
                var token = match.Value;
                if (token.Length < 2) continue;
                candidates.Add(token);
            }
        }
        return candidates;
    }

    public static (int Line, string Snippet)? FindSemicolonInsideApplyBracket(string text) {
        var searchStart = 0;
        while (true) {
            var applyIndex = text.IndexOf("@apply", searchStart, StringComparison.Ordinal);
            if (applyIndex < 0) return null;
            var depth = 0;
            var pos = applyIndex + "@apply".Length;
            while (pos < text.Length) {
                var c = text[pos];
                if (c == '[') {
                    depth++;
                } else if (c == ']') {
                    depth = Math.Max(0, depth - 1);
                } else if (c == ';') {
                    if (depth > 0) {
                        var line = 1;
                        for (var j = 0; j < pos; j++) {
                            if (text[j] == '\n') {
                                line++;
                            }
                        }
                        var snippetStart = Math.Max(applyIndex, pos - 40);
                        var snippet = text.Substring(snippetStart, pos - snippetStart + 1);
                        return (line, snippet);
                    }
                    break;
                }
                pos++;
            }
            searchStart = applyIndex + "@apply".Length;
        }
    }

    public static string StripApplyDirectives(string css) {
        var result = new StringBuilder(css.Length);
        var i = 0;
        while (true) {
            var applyIndex = css.IndexOf("@apply", i, StringComparison.Ordinal);
            if (applyIndex < 0) {
                result.Append(css, i, css.Length - i);
                return result.ToString();
            }
            result.Append(css, i, applyIndex - i);
            var depth = 0;
            var pos = applyIndex + "@apply".Length;
            var terminatorFound = false;
            while (pos < css.Length) {
                var c = css[pos];
                if (c == '[') {
                    depth++;
                } else if (c == ']') {
                    depth = Math.Max(0, depth - 1);
                } else if (c == ';' && depth == 0) {
                    pos++;
                    terminatorFound = true;
                    break;
                }
                pos++;
            }
            if (!terminatorFound) {
                result.Append(css, applyIndex, css.Length - applyIndex);
                return result.ToString();
            }
            i = pos;
        }
    }

    public static string UnwrapLayersAndSpliceRaw(string compiled, string raw, string spliceLayer = "components") {
        var result = new StringBuilder(compiled.Length + raw.Length + 16);
        var rawSpliced = false;
        var i = 0;
        while (i < compiled.Length) {
            var layerIndex = compiled.IndexOf("@layer", i, StringComparison.Ordinal);
            if (layerIndex < 0) {
                result.Append(compiled, i, compiled.Length - i);
                break;
            }
            result.Append(compiled, i, layerIndex - i);
            var headEnd = layerIndex + "@layer".Length;
            while (headEnd < compiled.Length && compiled[headEnd] != '{' && compiled[headEnd] != ';') {
                headEnd++;
            }
            if (headEnd >= compiled.Length) break;
            var layerName = compiled.Substring(layerIndex + "@layer".Length, headEnd - layerIndex - "@layer".Length).Trim();
            if (compiled[headEnd] == ';') {
                i = headEnd + 1;
                continue;
            }
            var depth = 1;
            var bodyStart = headEnd + 1;
            var pos = bodyStart;
            while (pos < compiled.Length && depth > 0) {
                var c = compiled[pos];
                if (c == '{') depth++;
                else if (c == '}') depth--;
                pos++;
            }
            var bodyEnd = pos - 1;
            result.Append(compiled, bodyStart, bodyEnd - bodyStart);
            if (!rawSpliced && layerName == spliceLayer) {
                result.Append('\n');
                result.Append(raw);
                result.Append('\n');
                rawSpliced = true;
            }
            i = pos;
        }
        if (!rawSpliced) {
            result.Append('\n');
            result.Append(raw);
        }
        return result.ToString();
    }
}
