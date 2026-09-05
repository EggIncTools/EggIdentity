using EggIdentity.Auth;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;

namespace EggIdentity.Host;

public sealed record AppAuthConfig(string Origin, AuthentikOAuth OAuth, string? EndSessionUrl = null);

public sealed class AppAuthConfigs(SettingsCache cache, string authority, string? fallbackDir) {
    private readonly Lazy<Dictionary<string, AppAuthConfig>> _fallback = new(() => LoadFallback(fallbackDir, authority));
    private SettingsSnapshot? _source;
    private Dictionary<string, AppAuthConfig> _current = [];

    public async Task<Dictionary<string, AppAuthConfig>> GetAsync(CancellationToken ct) {
        var snapshot = await cache.GetAsync(ct);
        if (ReferenceEquals(snapshot, _source)) return _current;

        var configs = FromSnapshot(snapshot, authority);
        if (configs.Count == 0 && !string.IsNullOrEmpty(fallbackDir)) configs = _fallback.Value;
        _current = configs;
        _source = snapshot;
        return configs;
    }

    public static Dictionary<string, AppAuthConfig> FromSnapshot(SettingsSnapshot snapshot, string authority) {
        ArgumentNullException.ThrowIfNull(snapshot);
        return FromRows(snapshot.Collection<AuthentikApp>(AuthentikApps.Key), authority);
    }

    public static Dictionary<string, AppAuthConfig> FromRows(IEnumerable<AuthentikApp> rows, string authority) {
        ArgumentNullException.ThrowIfNull(rows);
        var result = new Dictionary<string, AppAuthConfig>(StringComparer.Ordinal);
        foreach (var row in rows) {
            if (string.IsNullOrWhiteSpace(row.Origin)) continue;
            var oauth = new AuthentikOAuth(authority, row.ClientId, row.ClientSecret, row.CallbackUrl);
            result[row.Origin] = new AppAuthConfig(row.Origin, oauth, string.IsNullOrEmpty(row.EndSessionUrl) ? null : row.EndSessionUrl);
        }
        return result;
    }

    private static Dictionary<string, AppAuthConfig> LoadFallback(string? dir, string authority) {
        if (string.IsNullOrEmpty(dir)) return [];
        Console.Error.WriteLine(
            $"AUTHENTIK_APPS_DIR is deprecated: the authentik.apps collection is empty, loading {dir}. Run eggidentity-tools import-authentik-apps {dir} and drop the mount.");
        return AppAuthConfigLoader.LoadFromDirectory(dir, authority);
    }
}

public static class AppAuthConfigLoader {
    private static readonly string[] RequiredKeys = ["Origin", "ClientId", "ClientSecret", "CallbackUrl"];

    public static Dictionary<string, AppAuthConfig> LoadFromDirectory(string dirPath, string authentikAuthority) {
        var result = new Dictionary<string, AppAuthConfig>();
        foreach (var filePath in Directory.EnumerateFiles(dirPath)) {
            var values = ParseFile(filePath);
            var missing = RequiredKeys.Where(k => !values.ContainsKey(k)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException($"{filePath} missing required key(s): {string.Join(", ", missing)}");

            var oauth = new AuthentikOAuth(authentikAuthority, values["ClientId"], values["ClientSecret"], values["CallbackUrl"]);
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
