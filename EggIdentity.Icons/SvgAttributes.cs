using System.Text;

namespace EggIdentity.Icons;

internal static class SvgAttributes {
    private static readonly string[] KeptNames = ["xmlns", "viewBox", "fill"];

    internal static List<KeyValuePair<string, string>> Parse(ReadOnlySpan<char> tag) {
        var result = new List<KeyValuePair<string, string>>();
        var i = 0;
        while (i < tag.Length) {
            while (i < tag.Length && (char.IsWhiteSpace(tag[i]) || tag[i] == '/')) i++;
            if (i >= tag.Length) break;
            var nameStart = i;
            while (i < tag.Length && tag[i] != '=' && !char.IsWhiteSpace(tag[i])) i++;
            var name = tag[nameStart..i].ToString();
            while (i < tag.Length && tag[i] != '=') i++;
            i++;
            while (i < tag.Length && char.IsWhiteSpace(tag[i])) i++;
            if (i >= tag.Length) break;
            var quote = tag[i++];
            var valueStart = i;
            while (i < tag.Length && tag[i] != quote) i++;
            result.Add(new(name, tag[valueStart..i].ToString()));
            i++;
        }
        return result;
    }

    internal static string Render(List<KeyValuePair<string, string>> parsed, bool brand) {
        var kept = parsed.Where(a => IsKept(a.Key)).ToList();
        if (brand && !kept.Exists(a => a.Key == "fill")) kept.Add(new("fill", "currentColor"));
        if (brand && !kept.Exists(a => a.Key == "stroke")) kept.Add(new("stroke", "none"));
        kept.Add(new("width", "100%"));
        kept.Add(new("height", "100%"));
        kept.Add(new("aria-hidden", "true"));
        kept.Add(new("focusable", "false"));
        var sb = new StringBuilder();
        foreach (var (name, value) in kept) sb.Append(' ').Append(name).Append("=\"").Append(value).Append('"');
        return sb.ToString();
    }

    private static bool IsKept(string name) =>
        Array.IndexOf(KeptNames, name) >= 0 || name.StartsWith("stroke", StringComparison.Ordinal);
}
