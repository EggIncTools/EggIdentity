using EggIdentity.Contract;
using EggIdentity.Settings;

namespace EggIdentity.Fleet;

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
        var baseUrl = (snapshot.GetString(FleetSettings.PortainerApiUrl) ?? "").TrimEnd('/');
        var key = snapshot.GetString(FleetSettings.PortainerApiKey) ?? "";
        return baseUrl.Length == 0 || key.Length == 0 ? null : new PortainerConfig(baseUrl, key);
    }

    public HttpClient CreateClient(HttpMessageHandler? handler = null) {
        var http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = new Uri(BaseUrl + "/");
        http.Timeout = CallTimeout;
        http.DefaultRequestHeaders.Add("X-API-Key", ApiKey);
        return http;
    }

    public Uri WebhookUrl(string webhookId) => new(new Uri(BaseUrl + "/"), new Uri($"api/stacks/webhooks/{webhookId}", UriKind.Relative));
}

public static class StackLookup {
    public static (PortainerStack? Stack, string? Refusal) Find(IReadOnlyList<PortainerStack> stacks, string name) {
        ArgumentNullException.ThrowIfNull(stacks);
        if (string.IsNullOrWhiteSpace(name)) return (null, "no Portainer stack is named");
        var matches = stacks.Where(s => string.Equals(s.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count switch {
            0 => (null, $"Portainer has no stack named \"{name.Trim()}\""),
            1 => (matches[0], null),
            _ => (null, $"Portainer has {matches.Count} stacks named \"{name.Trim()}\" (ids {string.Join(", ", matches.Select(s => s.Id))}); rename all but one"),
        };
    }

    public static StackInfo Describe(PortainerStack stack) {
        ArgumentNullException.ThrowIfNull(stack);
        var armed = stack.AutoUpdate is { Webhook.Length: > 0 };
        var force = stack.AutoUpdate?.ForceUpdate == true;
        var label = $"stack \"{stack.Name}\" (Portainer #{stack.Id})";
        var refusal =
            stack.Type != PortainerStack.ComposeType ? $"{label} is not a compose stack"
            : !stack.GitBacked ? null
            : !armed ? $"{label} is git backed but has no GitOps webhook; enable the webhook under GitOps updates in Portainer"
            : !force ? $"{label} has force redeploy off, so its webhook only fires on a new commit; turn on force redeploy under GitOps updates in Portainer"
            : null;
        return new StackInfo(
            stack.Name, stack.Id, stack.EndpointId, stack.Name, stack.GitBacked,
            stack.GitConfig?.Url, stack.GitConfig?.ReferenceName, armed, force, refusal);
    }

    public static StackInfo Missing(string name, string refusal) =>
        new(name, 0, 0, null, false, null, null, false, false, refusal);
}
