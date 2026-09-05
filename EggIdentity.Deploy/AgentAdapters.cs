using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed class AgentEnvSource(AgentClient client, DeployOptions options) : IEnvSource {
    public Task<IReadOnlyList<EnvKeyInfo>> GetAsync(CancellationToken ct) => client.GetEnvAsync(options.AppName, ct);
}

public sealed class AgentRestartTrigger(AgentClient client, DeployOptions options) : IRestartTrigger {
    public Task<string?> RestartAsync(CancellationToken ct) => client.RestartAsync(options.AppName, ct);
}

public sealed class AgentStackEnvEditor(AgentClient client) : IStackEnvEditor {
    public Task<string?> ApplyAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken ct) => client.PatchStackEnvAsync(changes, ct);
}
