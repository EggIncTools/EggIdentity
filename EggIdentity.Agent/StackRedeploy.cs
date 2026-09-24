using System.Net;
using EggIdentity.Contract;
using EggIdentity.Deploy;

namespace EggIdentity.Agent;

public sealed record StackReadiness(StackInfo Info, Uri? WebhookUrl) {
    public bool Ready => Info.Ready;
}

public interface IStackRedeployer {
    Task<StackReadiness> ResolveAsync(DeployStack stack, CancellationToken ct);
    Task RedeployAsync(DeployStack stack, StackReadiness readiness, CancellationToken ct);
}

public sealed class StackRedeployer(HttpClient http, PortainerConfig? portainer) : IStackRedeployer {
    private const int BodyLimit = 300;

    public async Task<StackReadiness> ResolveAsync(DeployStack stack, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(stack);
        if (portainer is null)
            return new StackReadiness(Refuse(stack, "portainer.api_url and portainer.api_key are not set on the agent"), null);
        if (!stack.HasPortainerIds)
            return new StackReadiness(Refuse(stack, $"deploy.stacks row {stack.Name} has no stack_id and endpoint_id"), null);

        var remote = await portainer.CreateClient(http, stack).GetStackAsync(ct);
        var info = Describe(stack, remote);
        var url = info.Ready && remote.AutoUpdate is { Webhook.Length: > 0 } auto ? portainer.WebhookUrl(auto.Webhook) : null;
        return new StackReadiness(info, url);
    }

    public Task RedeployAsync(DeployStack stack, StackReadiness readiness, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(readiness);
        if (!readiness.Ready) throw new InvalidOperationException(readiness.Info.Refusal);
        return readiness.WebhookUrl is { } url
            ? InvokeWebhookAsync(url, ct)
            : (portainer ?? throw new InvalidOperationException("portainer is not configured"))
                .CreateClient(http, stack).RedeployAsync(forceRecreate: false, ct);
    }

    private async Task InvokeWebhookAsync(Uri url, CancellationToken ct) {
        using var response = await http.PostAsync(url, null, ct);
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Conflict) throw new StackBusyException("portainer is already redeploying this stack");
        var body = (await response.Content.ReadAsStringAsync(ct)).Trim();
        var detail = body.Length == 0 ? "" : ": " + (body.Length > BodyLimit ? body[..BodyLimit] : body);
        throw new InvalidOperationException($"portainer webhook returned {(int)response.StatusCode}{detail}");
    }

    public static StackInfo Describe(DeployStack row, PortainerStack stack) {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(stack);
        var armed = stack.AutoUpdate is { Webhook.Length: > 0 };
        var force = stack.AutoUpdate?.ForceUpdate == true;
        var label = $"stack \"{row.Name}\" (Portainer #{stack.Id} {stack.Name})";
        var refusal =
            stack.Type != PortainerStack.ComposeType ? $"{label} is not a compose stack"
            : !stack.GitBacked ? null
            : !armed ? $"{label} is git backed but has no GitOps webhook; enable the webhook under GitOps updates in Portainer"
            : !force ? $"{label} has force redeploy off, so its webhook only fires on a new commit; turn on force redeploy under GitOps updates in Portainer"
            : null;
        return new StackInfo(
            row.Name, row.StackId, row.EndpointId, stack.Name, stack.GitBacked,
            stack.GitConfig?.Url, stack.GitConfig?.ReferenceName, armed, force, refusal);
    }

    private static StackInfo Refuse(DeployStack row, string refusal) =>
        new(row.Name, row.StackId, row.EndpointId, null, false, null, null, false, false, refusal);
}
