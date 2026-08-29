using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EggIdentity.Host;

public interface IGitHubSponsorClient {
    Task<bool> IsSponsoredByUserIdAsync(string githubUserId, CancellationToken ct);
}

public sealed class GitHubSponsorClient(IHttpClientFactory httpClientFactory, string pat, string target) : IGitHubSponsorClient {
    public async Task<bool> IsSponsoredByUserIdAsync(string githubUserId, CancellationToken ct) {
        var login = await ResolveLoginAsync(githubUserId, ct);
        if (login is null) return false;
        return await CheckIsSponsoredByAsync(login, ct);
    }

    private async Task<string?> ResolveLoginAsync(string githubUserId, CancellationToken ct) {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/user/{githubUserId}");
        req.Headers.UserAgent.ParseAdd("EggIdentity-SponsorSync");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        var resp = await http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return ParseLogin(await resp.Content.ReadAsStringAsync(ct));
    }

    private async Task<bool> CheckIsSponsoredByAsync(string sponsorLogin, CancellationToken ct) {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        const string query = "query($login:String!,$sponsor:String!){user(login:$login){... on Sponsorable{isSponsoredBy(accountLogin:$sponsor)}}}";
        var payload = JsonSerializer.Serialize(new {
            query,
            variables = new { login = target, sponsor = sponsorLogin },
        });
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/graphql");
        req.Headers.UserAgent.ParseAdd("EggIdentity-SponsorSync");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return ParseIsSponsoredBy(await resp.Content.ReadAsStringAsync(ct));
    }

    public static string? ParseLogin(string json) {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("login", out var el) ? el.GetString() : null;
    }

    public static bool ParseIsSponsoredBy(string json) {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").GetProperty("user").GetProperty("isSponsoredBy").GetBoolean();
    }
}
