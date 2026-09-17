using EggIdentity.Auth;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;

namespace EggIdentity.Host;

public sealed record AppAuthConfig(string Origin, AuthentikOAuth OAuth, string? EndSessionUrl = null);

public sealed class AppAuthConfigs(SettingsCache cache, string authority, string? fallbackDir, string? tokenDecryptionKeyPem = null) {
    private readonly Lazy<Dictionary<string, AppAuthConfig>> _fallback = new(() => LoadFallback(fallbackDir, authority, tokenDecryptionKeyPem));
    private SettingsSnapshot? _source;
    private Dictionary<string, AppAuthConfig> _current = [];

    public async Task<Dictionary<string, AppAuthConfig>> GetAsync(CancellationToken ct) {
        var snapshot = await cache.GetAsync(ct);
        if (ReferenceEquals(snapshot, _source)) return _current;

        var configs = FromSnapshot(snapshot, authority, tokenDecryptionKeyPem);
        if (configs.Count == 0 && !string.IsNullOrEmpty(fallbackDir)) configs = _fallback.Value;
        _current = configs;
        _source = snapshot;
        return configs;
    }

    public static Dictionary<string, AppAuthConfig> FromSnapshot(
        SettingsSnapshot snapshot, string authority, string? tokenDecryptionKeyPem = null) {
        ArgumentNullException.ThrowIfNull(snapshot);
        return FromRows(snapshot.Collection<AuthentikApp>(AuthentikApps.Key), authority, tokenDecryptionKeyPem);
    }

    public static Dictionary<string, AppAuthConfig> FromRows(
        IEnumerable<AuthentikApp> rows, string authority, string? tokenDecryptionKeyPem = null) {
        ArgumentNullException.ThrowIfNull(rows);
        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Origin))
            .GroupBy(row => row.Origin, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => Build(g.Last(), authority, tokenDecryptionKeyPem),
                StringComparer.Ordinal);
    }

    private static AppAuthConfig Build(AuthentikApp row, string authority, string? tokenDecryptionKeyPem) {
        var oauth = new AuthentikOAuth(authority, row.ClientId, row.ClientSecret, row.CallbackUrl, tokenDecryptionKeyPem);
        return new AppAuthConfig(row.Origin, oauth, string.IsNullOrEmpty(row.EndSessionUrl) ? null : row.EndSessionUrl);
    }

    private static Dictionary<string, AppAuthConfig> LoadFallback(string? dir, string authority, string? tokenDecryptionKeyPem) {
        if (string.IsNullOrEmpty(dir)) return [];
        Console.Error.WriteLine(
            $"AUTHENTIK_APPS_DIR is deprecated: the authentik.apps collection is empty, loading {dir}. Run eggidentity-tools import-authentik-apps {dir} and drop the mount.");
        return AppAuthConfigLoader.LoadFromDirectory(dir, authority, tokenDecryptionKeyPem);
    }
}

public static class AppAuthConfigLoader {
    private static readonly string[] RequiredKeys = ["Origin", "ClientId", "ClientSecret", "CallbackUrl"];

    public static Dictionary<string, AppAuthConfig> LoadFromDirectory(
        string dirPath, string authentikAuthority, string? tokenDecryptionKeyPem = null) {
        var result = new Dictionary<string, AppAuthConfig>();
        foreach (var filePath in Directory.EnumerateFiles(dirPath)) {
            var values = ParseFile(filePath);
            var missing = RequiredKeys.Where(k => !values.ContainsKey(k)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"{filePath} missing required key(s): {string.Join(", ", missing)}");

            var oauth = new AuthentikOAuth(
                authentikAuthority, values["ClientId"], values["ClientSecret"], values["CallbackUrl"], tokenDecryptionKeyPem);
            result[values["Origin"]] = new AppAuthConfig(values["Origin"], oauth, values.GetValueOrDefault("EndSessionUrl"));
        }
        return result;
    }

    public static Dictionary<string, string> ParseFile(string filePath) {
        var values = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(filePath)) {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var idx = line.IndexOf('=');
            if (idx < 0) throw new InvalidOperationException($"{filePath}: malformed line \"{line}\"");
            values[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }
        return values;
    }
}
