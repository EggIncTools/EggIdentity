using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.Settings.AdminUi;

namespace EggIdentity.Host;

public sealed record AgentStackEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("masked")] bool Masked);

public sealed class AgentStackClient(
    IHttpClientFactory factory, SessionCookieOptions sessionOptions, string agentUrl, string appName)
    : IStackEnvSource, IRestartTrigger {

    public async Task<IReadOnlyList<string>> GetStackKeysAsync(CancellationToken ct) {
        using var client = Create();
        try {
            var entries = await client.GetFromJsonAsync<List<AgentStackEntry>>(
                new Uri("stack/env", UriKind.Relative), ct);
            return entries is null ? [] : [.. entries.Select(e => e.Name)];
        } catch (HttpRequestException) {
            return [];
        }
    }

    public async Task<string?> RestartAsync(CancellationToken ct) {
        using var client = Create();
        try {
            var response = await client.PostAsync(new Uri($"restart/{appName}", UriKind.Relative), null, ct);
            return response.IsSuccessStatusCode ? null : $"agent restart failed: {(int)response.StatusCode}";
        } catch (HttpRequestException e) {
            return $"agent restart failed: {e.Message}";
        }
    }

    private HttpClient Create() {
        var client = factory.CreateClient();
        client.BaseAddress = new Uri(agentUrl.TrimEnd('/') + "/");
        var token = SessionToken.Issue(
            sessionOptions,
            new SessionUser("eggidentity-host", null, UserRoles.ToName(UserRole.Admin)),
            DateTimeOffset.UtcNow);
        client.DefaultRequestHeaders.Add("Cookie", $"{sessionOptions.CookieName}={token}");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
