using EggIdentity.Auth;
using EggIdentity.Deploy;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;

namespace EggIdentity.Host;

public sealed record AppAuthConfig(string Origin, AuthentikOAuth OAuth, string? EndSessionUrl = null);

public sealed class AppAuthConfigs(SettingsCache cache, string authority, string? tokenDecryptionKeyPem = null) {
    private SettingsSnapshot? _source;
    private Dictionary<string, AppAuthConfig> _current = [];

    public async Task<Dictionary<string, AppAuthConfig>> GetAsync(CancellationToken ct) {
        var snapshot = await cache.GetAsync(ct);
        if (ReferenceEquals(snapshot, _source)) return _current;

        _current = FromSnapshot(snapshot, authority, tokenDecryptionKeyPem);
        _source = snapshot;
        return _current;
    }

    public static Dictionary<string, AppAuthConfig> FromSnapshot(
        SettingsSnapshot snapshot, string authority, string? tokenDecryptionKeyPem = null) {
        ArgumentNullException.ThrowIfNull(snapshot);
        return FromApps(snapshot.Collection<SuiteApp>(SuiteApps.Key), authority, tokenDecryptionKeyPem);
    }

    public static Dictionary<string, AppAuthConfig> FromApps(
        IEnumerable<SuiteApp> apps, string authority, string? tokenDecryptionKeyPem = null) {
        ArgumentNullException.ThrowIfNull(apps);
        return apps
            .Where(a => a.Enabled && a.HasLogin)
            .GroupBy(a => a.AuthOrigin ?? "", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Build(g.Last(), authority, tokenDecryptionKeyPem), StringComparer.Ordinal);
    }

    private static AppAuthConfig Build(SuiteApp app, string authority, string? tokenDecryptionKeyPem) {
        var oauth = new AuthentikOAuth(authority, app.AuthClientId ?? "", app.AuthClientSecret ?? "", app.AuthCallbackUrl ?? "", tokenDecryptionKeyPem);
        return new AppAuthConfig(app.AuthOrigin ?? "", oauth, string.IsNullOrWhiteSpace(app.AuthEndSessionUrl) ? null : app.AuthEndSessionUrl);
    }
}
