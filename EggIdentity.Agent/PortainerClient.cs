using System.Text;
using System.Text.Json;
using EggIdentity.Settings;

namespace EggIdentity.Agent;

public sealed record StackEnvEntry(string Name, string Value);

public sealed record StackEnvResult(bool Ok, string? Error, IReadOnlyList<StackEnvEntry> Entries);

public sealed record StackFileResult(bool Ok, string? Error, string Compose);

public sealed record PortainerConfig(string BaseUrl, string ApiKey, string StackId, string EndpointId) {
    public static PortainerConfig? FromSnapshot(SettingsSnapshot snapshot) {
        ArgumentNullException.ThrowIfNull(snapshot);
        var baseUrl = (snapshot.GetString(AgentSettings.PortainerApiUrl) ?? "").TrimEnd('/');
        var key = snapshot.GetString(AgentSettings.PortainerApiKey) ?? "";
        var stackId = snapshot.GetString(AgentSettings.PortainerStackId) ?? "";
        var endpointId = snapshot.GetString(AgentSettings.PortainerEndpointId) ?? "";
        if (baseUrl.Length == 0 || key.Length == 0 || stackId.Length == 0 || endpointId.Length == 0) return null;
        return new PortainerConfig(baseUrl, key, stackId, endpointId);
    }

    public PortainerClient CreateClient(HttpClient http) {
        ArgumentNullException.ThrowIfNull(http);
        http.BaseAddress = new Uri(BaseUrl + "/");
        http.DefaultRequestHeaders.Remove("X-API-Key");
        http.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        return new PortainerClient(http) { StackId = StackId, EndpointId = EndpointId };
    }
}

public sealed class PortainerClient(HttpClient http) {
    public string StackId { get; init; } = "";
    public string EndpointId { get; init; } = "";

    public async Task<StackEnvResult> GetEnvAsync(CancellationToken ct) {
        var response = await http.GetAsync(new Uri($"api/stacks/{StackId}", UriKind.Relative), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new StackEnvResult(false, $"GET stack {(int)response.StatusCode}", []);

        using var doc = JsonDocument.Parse(body);
        return new StackEnvResult(true, null, ReadEnv(doc.RootElement));
    }

    public async Task<StackFileResult> GetStackFileAsync(CancellationToken ct) {
        var response = await http.GetAsync(new Uri($"api/stacks/{StackId}/file", UriKind.Relative), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new StackFileResult(false, $"GET stack file {(int)response.StatusCode}", "");

        using var doc = JsonDocument.Parse(body);
        var compose = doc.RootElement.TryGetProperty("StackFileContent", out var content) ? content.GetString() ?? "" : "";
        return new StackFileResult(true, null, compose);
    }

    public async Task<StackEnvResult> PatchEnvAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(changes);

        var env = await GetEnvAsync(ct);
        if (!env.Ok) return env;
        var current = env.Entries.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);
        foreach (var (name, value) in changes) {
            if (value is null) current.Remove(name);
            else current[name] = value;
        }

        var file = await GetStackFileAsync(ct);
        if (!file.Ok) return new StackEnvResult(false, file.Error, []);

        var entries = current.Select(kv => new StackEnvEntry(kv.Key, kv.Value)).ToList();
        return await PutStackAsync(file.Compose, entries, ct);
    }

    public async Task<StackEnvResult> ReconcileAsync(CancellationToken ct) {
        var env = await GetEnvAsync(ct);
        if (!env.Ok) return env;
        var file = await GetStackFileAsync(ct);
        if (!file.Ok) return new StackEnvResult(false, file.Error, []);
        return await PutStackAsync(file.Compose, env.Entries, ct);
    }

    private async Task<StackEnvResult> PutStackAsync(string compose, IReadOnlyList<StackEnvEntry> env, CancellationToken ct) {
        var payload = new Dictionary<string, object?> {
            ["stackFileContent"] = compose,
            ["pullImage"] = false,
            ["prune"] = false,
            ["env"] = env.Select(e => new Dictionary<string, string> {
                ["name"] = e.Name,
                ["value"] = e.Value,
            }).ToList(),
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var put = await http.PutAsync(
            new Uri($"api/stacks/{StackId}?endpointId={EndpointId}", UriKind.Relative), content, ct);
        if (!put.IsSuccessStatusCode)
            return new StackEnvResult(false, $"PUT stack {(int)put.StatusCode}", []);

        return new StackEnvResult(true, null, env);
    }

    private static List<StackEnvEntry> ReadEnv(JsonElement stack) {
        var entries = new List<StackEnvEntry>();
        if (!stack.TryGetProperty("Env", out var env) || env.ValueKind != JsonValueKind.Array) return entries;
        foreach (var item in env.EnumerateArray()) {
            if (!item.TryGetProperty("name", out var name)) continue;
            var value = item.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
            entries.Add(new StackEnvEntry(name.GetString() ?? "", value));
        }
        return entries;
    }
}
