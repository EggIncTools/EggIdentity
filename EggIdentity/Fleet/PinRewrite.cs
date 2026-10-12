using System.Text.RegularExpressions;

namespace EggIdentity.Fleet;

public static partial class PinRewrite {
    [GeneratedRegex("""<PackageVersion\s+Include="(?<id>[^"]+)"\s+Version="(?<v>[^"]*)"\s*/>""")]
    private static partial Regex Line();

    [GeneratedRegex("""^\$\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)\)$""")]
    private static partial Regex PropertyRef();

    public static IReadOnlyList<string> Read(string props, string prefix) =>
        [.. Line().Matches(props)
            .Where(m => m.Groups["id"].Value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(m => Resolve(props, m.Groups["v"].Value))
            .Distinct(StringComparer.Ordinal)];

    public static string Apply(string props, string prefix, string version) {
        var dropped = new HashSet<string>(StringComparer.Ordinal);
        var text = Line().Replace(props, m => {
            if (!m.Groups["id"].Value.StartsWith(prefix, StringComparison.Ordinal)) return m.Value;
            if (PropertyRef().Match(m.Groups["v"].Value) is { Success: true } p) dropped.Add(p.Groups["name"].Value);
            var v = m.Groups["v"];
            return string.Concat(m.Value.AsSpan(0, v.Index - m.Index), version, m.Value.AsSpan(v.Index - m.Index + v.Length));
        });
        foreach (var name in dropped.Where(n => !text.Contains($"$({n})", StringComparison.Ordinal)))
            text = Regex.Replace(text, $@"[ \t]*<{Regex.Escape(name)}>[^<]*</{Regex.Escape(name)}>\r?\n?", "");
        return text;
    }

    private static string Resolve(string props, string value) {
        if (PropertyRef().Match(value) is not { Success: true } p) return value;
        var name = Regex.Escape(p.Groups["name"].Value);
        return Regex.Match(props, $"<{name}>(?<v>[^<]*)</{name}>") is { Success: true } d ? d.Groups["v"].Value.Trim() : value;
    }
}
