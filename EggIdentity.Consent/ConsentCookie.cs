using System.Globalization;

namespace EggIdentity.Consent;

public static class ConsentCookie {
    public const string Name = "eggidentity_consent";

    public static string Format(ConsentState state) =>
        string.Create(CultureInfo.InvariantCulture,
            $"v={state.PolicyVersion}&f={(state.Functional ? 1 : 0)}&a={(state.Analytics ? 1 : 0)}&t={state.DecidedAt.ToUnixTimeSeconds()}");

    public static ConsentState? Parse(string? value, int minPolicyVersion) {
        if (string.IsNullOrEmpty(value)) return null;
        int? version = null, functional = null, analytics = null;
        long? unix = null;
        foreach (var pair in value.Split('&')) {
            var eq = pair.IndexOf('=');
            if (eq <= 0) return null;
            var key = pair[..eq];
            var raw = pair[(eq + 1)..];
            switch (key) {
                case "v": version = ParseInt(raw); break;
                case "f": functional = ParseInt(raw); break;
                case "a": analytics = ParseInt(raw); break;
                case "t": unix = long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var t) ? t : null; break;
                default: return null;
            }
        }
        if (version is null || functional is null || analytics is null || unix is null) return null;
        return version.Value < minPolicyVersion
            ? null
            : new ConsentState(functional.Value == 1, analytics.Value == 1, version.Value, DateTimeOffset.FromUnixTimeSeconds(unix.Value));
    }

    private static int? ParseInt(string raw) =>
        int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
}
