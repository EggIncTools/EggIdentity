using System.Text;
using System.Text.Json;

namespace EggIdentity.Agent;

public sealed record StackEnvEntry(string Name, string Value);

public sealed record StackEnvResult(bool Ok, string? Error, IReadOnlyList<StackEnvEntry> Entries);

public sealed class PortainerClient(HttpClient http) {
    public static PortainerClient? FromEnvironment(HttpClient http) {
        var baseUrl = (Environment.GetEnvironmentVariable("PORTAINER_API_URL") ?? "").TrimEnd('/');
        var key = Environment.GetEnvironmentVariable("PORTAINER_API_KEY") ?? "";
        var stackId = Environment.GetEnvironmentVariable("PORTAINER_STACK_ID") ?? "";
        var endpointId = Environment.GetEnvironmentVariable("PORTAINER_ENDPOINT_ID") ?? "";
        if (baseUrl.Length == 0 || key.Length == 0 || stackId.Length == 0 || endpointId.Length == 0) return null;

        http.BaseAddress = new Uri(baseUrl + "/");
        http.DefaultRequestHeaders.Remove("X-API-Key");
        http.DefaultRequestHeaders.Add("X-API-Key", key);
        return new PortainerClient(http) { StackId = stackId, EndpointId = endpointId };
    }

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

    public async Task<StackEnvResult> PatchEnvAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(changes);

        var response = await http.GetAsync(new Uri($"api/stacks/{StackId}", UriKind.Relative), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new StackEnvResult(false, $"GET stack {(int)response.StatusCode}", []);

        using var doc = JsonDocument.Parse(body);
        var current = ReadEnv(doc.RootElement).ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);

        foreach (var (name, value) in changes) {
            if (value is null) current.Remove(name);
            else current[name] = value;
        }

        var fileResponse = await http.GetAsync(new Uri($"api/stacks/{StackId}/file", UriKind.Relative), ct);
        var fileBody = await fileResponse.Content.ReadAsStringAsync(ct);
        if (!fileResponse.IsSuccessStatusCode)
            return new StackEnvResult(false, $"GET stack file {(int)fileResponse.StatusCode}", []);

        using var fileDoc = JsonDocument.Parse(fileBody);
        var compose = fileDoc.RootElement.GetProperty("StackFileContent").GetString() ?? "";

        var payload = new Dictionary<string, object?> {
            ["stackFileContent"] = compose,
            ["pullImage"] = false,
            ["prune"] = false,
            ["env"] = current.Select(kv => new Dictionary<string, string> {
                ["name"] = kv.Key,
                ["value"] = kv.Value,
            }).ToList(),
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var put = await http.PutAsync(
            new Uri($"api/stacks/{StackId}?endpointId={EndpointId}", UriKind.Relative), content, ct);
        if (!put.IsSuccessStatusCode)
            return new StackEnvResult(false, $"PUT stack {(int)put.StatusCode}", []);

        return new StackEnvResult(true, null, [.. current.Select(kv => new StackEnvEntry(kv.Key, kv.Value))]);
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
