using System.Net;
using System.Text;
using System.Text.Json;
using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Agent;

public sealed record StackEnvEntry(string Name, string Value);

public sealed record PortainerAutoUpdate(string Webhook, bool ForceUpdate, bool ForcePullImage);

public sealed record PortainerGitConfig(string Url, string ReferenceName, string ConfigFilePath, bool TlsSkipVerify, string? Username);

public sealed record PortainerStack(
    int Id,
    string Name,
    int Type,
    int EndpointId,
    IReadOnlyList<StackEnvEntry> Env,
    PortainerAutoUpdate? AutoUpdate,
    PortainerGitConfig? GitConfig,
    bool Prune) {
    public const int ComposeType = 2;

    public bool GitBacked => GitConfig is not null;
}

public sealed class StackBusyException(string message) : Exception(message);

public sealed record PortainerConfig(string BaseUrl, string ApiKey) {
    public static readonly TimeSpan CallTimeout = TimeSpan.FromMinutes(10);

    public static PortainerConfig? FromSnapshot(SettingsSnapshot snapshot) {
        ArgumentNullException.ThrowIfNull(snapshot);
        var baseUrl = (snapshot.GetString(AgentSettings.PortainerApiUrl) ?? "").TrimEnd('/');
        var key = snapshot.GetString(AgentSettings.PortainerApiKey) ?? "";
        if (baseUrl.Length == 0 || key.Length == 0) return null;
        return new PortainerConfig(baseUrl, key);
    }

    public HttpClient Configure(HttpClient http) {
        ArgumentNullException.ThrowIfNull(http);
        if (http.BaseAddress is not null) return http;
        http.BaseAddress = new Uri(BaseUrl + "/");
        http.Timeout = CallTimeout;
        http.DefaultRequestHeaders.Remove("X-API-Key");
        http.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        return http;
    }

    public PortainerClient CreateClient(HttpClient http, DeployStack stack) {
        ArgumentNullException.ThrowIfNull(stack);
        return new PortainerClient(Configure(http), stack.StackId, stack.EndpointId);
    }

    public Uri WebhookUrl(string webhookId) => new(new Uri(BaseUrl + "/"), new Uri($"api/stacks/webhooks/{webhookId}", UriKind.Relative));
}

public sealed class PortainerClient(HttpClient http, int stackId, int endpointId) {
    private const int BodyLimit = 300;

    public int StackId => stackId;

    public int EndpointId => endpointId;

    public async Task<PortainerStack> GetStackAsync(CancellationToken ct) {
        using var response = await http.GetAsync(new Uri($"api/stacks/{stackId}", UriKind.Relative), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw Failure($"GET stack {stackId}", response, body);
        using var doc = JsonDocument.Parse(body);
        return ReadStack(doc.RootElement);
    }

    public async Task<string> GetStackFileAsync(CancellationToken ct) {
        using var response = await http.GetAsync(new Uri($"api/stacks/{stackId}/file", UriKind.Relative), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw Failure($"GET stack {stackId} file", response, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("StackFileContent", out var content) ? content.GetString() ?? "" : "";
    }

    public async Task<PortainerStack> UpdateEnvAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(changes);
        var stack = await GetStackAsync(ct);
        await RedeployAsync(stack, MergeEnv(stack.Env, changes), forceRecreate: true, ct);
        return stack;
    }

    public async Task<PortainerStack> RedeployAsync(bool forceRecreate, CancellationToken ct) {
        var stack = await GetStackAsync(ct);
        await RedeployAsync(stack, stack.Env, forceRecreate, ct);
        return stack;
    }

    internal static List<StackEnvEntry> MergeEnv(IReadOnlyList<StackEnvEntry> current, IReadOnlyDictionary<string, string?> changes) {
        var merged = new List<StackEnvEntry>(current.Count + changes.Count);
        foreach (var entry in current) {
            if (!changes.TryGetValue(entry.Name, out var value)) merged.Add(entry);
            else if (value is not null) merged.Add(entry with { Value = value });
        }
        foreach (var (name, value) in changes) {
            if (value is null || current.Any(e => string.Equals(e.Name, name, StringComparison.Ordinal))) continue;
            merged.Add(new StackEnvEntry(name, value));
        }
        return merged;
    }

    private Task RedeployAsync(PortainerStack stack, IReadOnlyList<StackEnvEntry> env, bool forceRecreate, CancellationToken ct) {
        if (stack.Type != PortainerStack.ComposeType)
            throw new InvalidOperationException($"stack {stackId} ({stack.Name}) has type {stack.Type}; only compose stacks are supported");
        return stack.GitConfig is { } git
            ? RedeployGitAsync(git, env, stack.Prune, ct)
            : RedeployFileAsync(env, stack.Prune, forceRecreate, ct);
    }

    private Task RedeployGitAsync(PortainerGitConfig git, IReadOnlyList<StackEnvEntry> env, bool prune, CancellationToken ct) {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) {
            ["RepositoryReferenceName"] = git.ReferenceName,
            ["RepositoryAuthentication"] = git.Username is not null,
            ["RepositoryUsername"] = git.Username ?? "",
            ["RepositoryPassword"] = "",
            ["Env"] = WireEnv(env),
            ["Prune"] = prune,
            ["RepullImageAndRedeploy"] = false,
        };
        return SendAsync($"api/stacks/{stackId}/git/redeploy?endpointId={endpointId}", payload, "PUT stack git/redeploy", ct);
    }

    private async Task RedeployFileAsync(IReadOnlyList<StackEnvEntry> env, bool prune, bool forceRecreate, CancellationToken ct) {
        var compose = await GetStackFileAsync(ct);
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) {
            ["StackFileContent"] = compose,
            ["Env"] = WireEnv(env),
            ["Prune"] = prune,
            ["RepullImageAndRedeploy"] = forceRecreate,
        };
        await SendAsync($"api/stacks/{stackId}?endpointId={endpointId}", payload, "PUT stack", ct);
    }

    private async Task SendAsync(string path, Dictionary<string, object?> payload, string what, CancellationToken ct) {
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await http.PutAsync(new Uri(path, UriKind.Relative), content, ct);
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Conflict) throw new StackBusyException("portainer is already redeploying this stack");
        throw Failure(what, response, await response.Content.ReadAsStringAsync(ct));
    }

    private static List<Dictionary<string, string>> WireEnv(IReadOnlyList<StackEnvEntry> env) =>
        [.. env.Select(e => new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = e.Name, ["value"] = e.Value })];

    private static InvalidOperationException Failure(string what, HttpResponseMessage response, string body) {
        var detail = body.Trim();
        if (detail.Length > BodyLimit) detail = detail[..BodyLimit];
        return new InvalidOperationException(detail.Length == 0
            ? $"portainer {what} returned {(int)response.StatusCode}"
            : $"portainer {what} returned {(int)response.StatusCode}: {detail}");
    }

    internal static PortainerStack ReadStack(JsonElement root) =>
        new(
            Int(root, "Id"),
            Str(root, "Name") ?? "",
            Int(root, "Type"),
            Int(root, "EndpointId"),
            ReadEnv(root),
            ReadAutoUpdate(Prop(root, "AutoUpdate")),
            ReadGit(Prop(root, "GitConfig")),
            Prop(root, "Option") is { } option && Bool(option, "Prune"));

    private static PortainerAutoUpdate? ReadAutoUpdate(JsonElement? element) =>
        element is { } e ? new PortainerAutoUpdate(Str(e, "Webhook") ?? "", Bool(e, "ForceUpdate"), Bool(e, "ForcePullImage")) : null;

    private static PortainerGitConfig? ReadGit(JsonElement? element) {
        if (element is not { } e) return null;
        var username = Prop(e, "Authentication") is { } auth ? Str(auth, "Username") : null;
        return new PortainerGitConfig(
            Str(e, "URL") ?? "", Str(e, "ReferenceName") ?? "", Str(e, "ConfigFilePath") ?? "", Bool(e, "TLSSkipVerify"),
            string.IsNullOrEmpty(username) ? null : username);
    }

    private static List<StackEnvEntry> ReadEnv(JsonElement stack) {
        if (Prop(stack, "Env") is not { ValueKind: JsonValueKind.Array } env) return [];
        return [.. env.EnumerateArray()
            .Select(item => (Name: Str(item, "name") ?? "", Value: Str(item, "value") ?? ""))
            .Where(pair => pair.Name.Length > 0)
            .Select(pair => new StackEnvEntry(pair.Name, pair.Value))];
    }

    private static JsonElement? Prop(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? v : null;

    private static string? Str(JsonElement e, string name) => Prop(e, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static int Int(JsonElement e, string name) => Prop(e, name) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out var i) ? i : 0;

    private static bool Bool(JsonElement e, string name) => Prop(e, name) is { ValueKind: JsonValueKind.True };
}
