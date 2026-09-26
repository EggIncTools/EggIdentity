using System.Net.Http.Headers;
using System.Text.Json;
using EggIdentity.Contract;

namespace EggIdentity.Host;

public sealed record AuthentikSourceConnection(long Pk, string SourceSlug, string SourceName, string Identifier) {
    public string? Provider => AuthentikAdminClient.MapProvider(SourceSlug, SourceName);
}

public interface IAuthentikAdminClient {
    Task<string?> FindUserPkAsync(string username, string subject, CancellationToken ct);
    Task<IReadOnlyList<AuthentikSourceConnection>> ListConnectionsAsync(string userPk, CancellationToken ct);
    Task DeleteConnectionAsync(long connectionPk, CancellationToken ct);
}

public sealed class AuthentikAdminClient(IHttpClientFactory httpClientFactory, string authority, string apiToken) : IAuthentikAdminClient {
    private readonly string _base = $"{authority.TrimEnd('/')}/api/v3";

    public async Task<string?> FindUserPkAsync(string username, string subject, CancellationToken ct) {
        var json = await GetAsync($"/core/users/?username={Uri.EscapeDataString(username)}&page_size=10", ct);
        return json is null ? null : ParseUserPk(json, subject);
    }

    public async Task<IReadOnlyList<AuthentikSourceConnection>> ListConnectionsAsync(string userPk, CancellationToken ct) {
        var json = await GetAsync($"/sources/user_connections/all/?user={Uri.EscapeDataString(userPk)}&page_size=100", ct);
        return json is null ? [] : ParseConnections(json);
    }

    public async Task DeleteConnectionAsync(long connectionPk, CancellationToken ct) {
        var http = Create();
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"{_base}/sources/user_connections/all/{connectionPk}/");
        var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return;
        resp.EnsureSuccessStatusCode();
    }

    private async Task<string?> GetAsync(string path, CancellationToken ct) {
        var http = Create();
        using var req = new HttpRequestMessage(HttpMethod.Get, _base + path);
        var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    private HttpClient Create() {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    public static string? ParseUserPk(string json, string subject) {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return null;
        foreach (var user in results.EnumerateArray()) {
            if (!MatchesSubject(user, subject)) continue;
            return user.TryGetProperty("pk", out var pk) ? pk.GetRawText() : null;
        }
        return null;
    }

    private static bool MatchesSubject(JsonElement user, string subject) {
        foreach (var field in (string[])["uid", "pk", "uuid", "email", "username"]) {
            if (!user.TryGetProperty(field, out var el)) continue;
            var value = el.ValueKind switch {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                _ => null,
            };
            if (value is not null && string.Equals(value, subject, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    public static IReadOnlyList<AuthentikSourceConnection> ParseConnections(string json) {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return [];
        var list = new List<AuthentikSourceConnection>();
        foreach (var row in results.EnumerateArray()) {
            if (!row.TryGetProperty("pk", out var pkEl) || !pkEl.TryGetInt64(out var pk)) continue;
            var identifier = row.TryGetProperty("identifier", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
            if (string.IsNullOrEmpty(identifier)) continue;
            var slug = "";
            var name = "";
            if (row.TryGetProperty("source_obj", out var source) && source.ValueKind == JsonValueKind.Object) {
                slug = source.TryGetProperty("slug", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() ?? "" : "";
                name = source.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            }
            list.Add(new AuthentikSourceConnection(pk, slug, name, identifier));
        }
        return list;
    }

    public static string? MapProvider(string slug, string name) {
        foreach (var provider in IdentityWire.KnownProviders) {
            if (string.Equals(slug, provider, StringComparison.OrdinalIgnoreCase)) return provider;
            if (string.Equals(name, provider, StringComparison.OrdinalIgnoreCase)) return provider;
        }
        foreach (var provider in IdentityWire.KnownProviders) {
            if (slug.StartsWith(provider + "-", StringComparison.OrdinalIgnoreCase)) return provider;
            if (slug.EndsWith("-" + provider, StringComparison.OrdinalIgnoreCase)) return provider;
        }
        return null;
    }
}
