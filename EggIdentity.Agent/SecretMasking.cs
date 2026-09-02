namespace EggIdentity.Agent;

public static class SecretMasking {
    private static readonly string[] Markers =
        ["SECRET", "TOKEN", "PASSWORD", "PASSWD", "APIKEY", "API_KEY", "_KEY", "KEY_", "SALT", "PAT", "CREDENTIAL"];

    public static bool LooksSecret(string key) {
        if (string.IsNullOrEmpty(key)) return false;
        var upper = key.ToUpperInvariant();
        foreach (var marker in Markers) {
            if (upper.Contains(marker, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    public static string Mask(string key, string value) {
        if (!LooksSecret(key)) return value;
        return value.Length == 0 ? "" : "********";
    }
}
