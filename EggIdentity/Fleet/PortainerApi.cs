using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace EggIdentity.Fleet;

public interface IPortainer {
    Task<IReadOnlyList<PortainerStack>> ListStacksAsync(CancellationToken ct);
    Task<string> GetStackFileAsync(PortainerStack stack, CancellationToken ct);
    Task RedeployAsync(PortainerStack stack, CancellationToken ct);
    Task UpdateEnvAsync(PortainerStack stack, IReadOnlyDictionary<string, string?> changes, CancellationToken ct);
    IDockerEngine Docker(int endpointId);
}

public sealed class PortainerApi(HttpClient http, PortainerConfig config, TimeSpan dockerCallTimeout, Func<TimeSpan> pullTimeout) : IPortainer {
    private const int BodyLimit = 300;

    public async Task<IReadOnlyList<PortainerStack>> ListStacksAsync(CancellationToken ct) {
        using var response = await http.GetAsync(new Uri("api/stacks", UriKind.Relative), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw Failure("GET stacks", response, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.ValueKind != JsonValueKind.Array ? [] : [.. doc.RootElement.EnumerateArray().Select(ReadStack)];
    }

    public async Task<string> GetStackFileAsync(PortainerStack stack, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(stack);
        using var response = await http.GetAsync(new Uri($"api/stacks/{Id(stack.Id)}/file", UriKind.Relative), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw Failure($"GET stack {stack.Id} file", response, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("StackFileContent", out var content) ? content.GetString() ?? "" : "";
    }

    public async Task RedeployAsync(PortainerStack stack, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(stack);
        var current = await GetStackAsync(stack.Id, ct);
        if (StackLookup.Describe(current).Refusal is { } refusal) throw new InvalidOperationException(refusal);
        if (current.GitBacked) {
            await InvokeWebhookAsync(config.WebhookUrl(current.AutoUpdate!.Webhook), ct);
            return;
        }
        await PutFileStackAsync(current, current.Env, forceRecreate: false, ct);
    }

    public async Task UpdateEnvAsync(PortainerStack stack, IReadOnlyDictionary<string, string?> changes, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(changes);
        var current = await GetStackAsync(stack.Id, ct);
        if (current.Type != PortainerStack.ComposeType)
            throw new InvalidOperationException($"stack {current.Id} ({current.Name}) has type {current.Type}; only compose stacks are supported");
        var env = MergeEnv(current.Env, changes);
        if (current.GitConfig is { } git) await PutGitRedeployAsync(current, git, env, ct);
        else await PutFileStackAsync(current, env, forceRecreate: true, ct);
    }

    public IDockerEngine Docker(int endpointId) =>
        new DockerEngineClient(http, $"api/endpoints/{Id(endpointId)}/docker/", dockerCallTimeout, pullTimeout);

    public async Task<PortainerStack> GetStackAsync(int stackId, CancellationToken ct) {
        using var response = await http.GetAsync(new Uri($"api/stacks/{Id(stackId)}", UriKind.Relative), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw Failure($"GET stack {stackId}", response, body);
        using var doc = JsonDocument.Parse(body);
        return ReadStack(doc.RootElement);
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

    private async Task InvokeWebhookAsync(Uri url, CancellationToken ct) {
        using var response = await http.PostAsync(url, null, ct);
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Conflict) throw new StackBusyException("portainer is already redeploying this stack");
        var body = (await response.Content.ReadAsStringAsync(ct)).Trim();
        var detail = body.Length == 0 ? "" : ": " + (body.Length > BodyLimit ? body[..BodyLimit] : body);
        throw new InvalidOperationException($"portainer webhook returned {(int)response.StatusCode}{detail}");
    }

    private Task PutGitRedeployAsync(PortainerStack stack, PortainerGitConfig git, IReadOnlyList<StackEnvEntry> env, CancellationToken ct) {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) {
            ["RepositoryReferenceName"] = git.ReferenceName,
            ["RepositoryAuthentication"] = git.Username is not null,
            ["RepositoryUsername"] = git.Username ?? "",
            ["RepositoryPassword"] = "",
            ["Env"] = WireEnv(env),
            ["Prune"] = stack.Prune,
            ["RepullImageAndRedeploy"] = false,
        };
        return PutAsync($"api/stacks/{Id(stack.Id)}/git/redeploy?endpointId={Id(stack.EndpointId)}", payload, "PUT stack git/redeploy", ct);
    }

    private async Task PutFileStackAsync(PortainerStack stack, IReadOnlyList<StackEnvEntry> env, bool forceRecreate, CancellationToken ct) {
        var compose = await GetStackFileAsync(stack, ct);
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) {
            ["StackFileContent"] = compose,
            ["Env"] = WireEnv(env),
            ["Prune"] = stack.Prune,
            ["RepullImageAndRedeploy"] = forceRecreate,
        };
        await PutAsync($"api/stacks/{Id(stack.Id)}?endpointId={Id(stack.EndpointId)}", payload, "PUT stack", ct);
    }

    private async Task PutAsync(string path, Dictionary<string, object?> payload, string what, CancellationToken ct) {
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await http.PutAsync(new Uri(path, UriKind.Relative), content, ct);
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Conflict) throw new StackBusyException("portainer is already redeploying this stack");
        throw Failure(what, response, await response.Content.ReadAsStringAsync(ct));
    }

    private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

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

    private static List<StackEnvEntry> ReadEnv(JsonElement stack) =>
        Prop(stack, "Env") is not { ValueKind: JsonValueKind.Array } env
            ? []
            : [.. env.EnumerateArray()
                .Select(item => (Name: Str(item, "name") ?? "", Value: Str(item, "value") ?? ""))
                .Where(pair => pair.Name.Length > 0)
                .Select(pair => new StackEnvEntry(pair.Name, pair.Value))];

    private static JsonElement? Prop(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? v : null;

    private static string? Str(JsonElement e, string name) => Prop(e, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static int Int(JsonElement e, string name) => Prop(e, name) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt32(out var i) ? i : 0;

    private static bool Bool(JsonElement e, string name) => Prop(e, name) is { ValueKind: JsonValueKind.True };
}
