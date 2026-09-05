using System.Collections.Frozen;
using System.Reflection;
using System.Text;

namespace EggIdentity.Icons;

public static class IconPack {
    public const string BrandPrefix = "brand-";
    private const string BrandFolder = "simple-icons";
    private const string SvgExtension = ".svg";

    private static readonly Lazy<FrozenDictionary<string, string>> Svgs = new(Load);
    private static readonly Lazy<FrozenSet<string>> NameSet = new(() => Svgs.Value.Keys.ToFrozenSet(StringComparer.Ordinal));

    public static IReadOnlySet<string> Names => NameSet.Value;

    public static IReadOnlyDictionary<string, string> Aliases { get; } = new Dictionary<string, string>(StringComparer.Ordinal) {
        ["close"] = "x",
        ["warning"] = "triangle-alert",
        ["alert"] = "triangle-alert",
        ["gear"] = "settings",
        ["doc"] = "file-text",
        ["log"] = "scroll-text",
        ["filter"] = "funnel",
        ["swap"] = "arrow-left-right",
        ["refresh"] = "refresh-cw",
        ["trash-2"] = "trash",
        ["chevronDown"] = "chevron-down",
        ["chevronUp"] = "chevron-up",
        ["chevronLeft"] = "chevron-left",
        ["chevronRight"] = "chevron-right",
        ["device"] = "smartphone",
        ["home"] = "house",
        ["android"] = "brand-android",
        ["apple"] = "brand-apple",
        ["github"] = "brand-github",
        ["discord"] = "brand-discord",
        ["google"] = "brand-google",
        ["postman"] = "send",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool TryGet(string name, out string svg) {
        var resolved = Aliases.GetValueOrDefault(name, name);
        return Svgs.Value.TryGetValue(resolved, out svg!);
    }

    public static string? Get(string name) => TryGet(name, out var svg) ? svg : null;

    private static FrozenDictionary<string, string> Load() {
        var assembly = typeof(IconPack).Assembly;
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var resource in assembly.GetManifestResourceNames()) {
            if (!resource.EndsWith(SvgExtension, StringComparison.Ordinal)) continue;
            var slash = resource.IndexOf('/');
            var folder = resource[..slash];
            var file = resource[(slash + 1)..^SvgExtension.Length];
            var brand = folder == BrandFolder;
            result[brand ? BrandPrefix + file : file] = Normalize(ReadResource(assembly, resource), brand);
        }
        return result.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static string ReadResource(Assembly assembly, string resource) {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Normalize(string raw, bool brand) {
        var start = raw.IndexOf("<svg", StringComparison.Ordinal);
        var end = raw.IndexOf('>', start);
        var attributes = SvgAttributes.Parse(raw.AsSpan(start + 4, end - start - 4));
        var body = StripTitle(raw[(end + 1)..]);
        return "<svg" + SvgAttributes.Render(attributes, brand) + ">" + body;
    }

    private static string StripTitle(string body) {
        var open = body.IndexOf("<title", StringComparison.Ordinal);
        if (open < 0) return body;
        var close = body.IndexOf("</title>", open, StringComparison.Ordinal);
        return close < 0 ? body : body.Remove(open, close + "</title>".Length - open);
    }
}
