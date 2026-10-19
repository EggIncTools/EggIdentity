using System.Text.Json;

namespace EggIdentity.UI.Suite;

public static class SuiteHandoffRules {
    public static string Build(IReadOnlyList<SuiteEntry> targets) {
        ArgumentNullException.ThrowIfNull(targets);
        var urls = targets.Where(t => SuiteCatalog.IsAllowedReturn(t.Url)).Select(t => t.Url.TrimEnd('/') + "/").ToArray();
        var rules = new { prerender = new[] { new { source = "list", urls, eagerness = "moderate" } } };
        return JsonSerializer.Serialize(rules).Replace("<", "\\u003c", StringComparison.Ordinal);
    }
}
