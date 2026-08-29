using System.Net.Http.Headers;
using System.Text.Json;

namespace EggIdentity.Host;

public interface IDiscordRoleClient {
    Task AddRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct);
    Task RemoveRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct);
    Task<bool> HasRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct);
}

public sealed class DiscordRoleClient(IHttpClientFactory httpClientFactory, string botToken) : IDiscordRoleClient {
    public Task AddRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, guildId, discordUserId, roleId, ct);

    public Task RemoveRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, guildId, discordUserId, roleId, ct);

    public async Task<bool> HasRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct) {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildMemberUrl(guildId, discordUserId));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return false;
        return ParseHasRole(await resp.Content.ReadAsStringAsync(ct), roleId);
    }

    private async Task SendAsync(HttpMethod method, string guildId, string discordUserId, string roleId, CancellationToken ct) {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        using var req = new HttpRequestMessage(method, BuildRoleUrl(guildId, discordUserId, roleId));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
    }

    public static string BuildRoleUrl(string guildId, string discordUserId, string roleId) =>
        $"https://discord.com/api/v10/guilds/{guildId}/members/{discordUserId}/roles/{roleId}";

    public static string BuildMemberUrl(string guildId, string discordUserId) =>
        $"https://discord.com/api/v10/guilds/{guildId}/members/{discordUserId}";

    public static bool ParseHasRole(string memberJson, string roleId) {
        try {
            using var doc = JsonDocument.Parse(memberJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty("roles", out var roles)) return false;
            if (roles.ValueKind != JsonValueKind.Array) return false;
            foreach (var r in roles.EnumerateArray()) {
                if (r.ValueKind == JsonValueKind.String && r.GetString() == roleId)
                    return true;
            }

            return false;
        } catch (JsonException) {
            return false;
        }
    }
}
