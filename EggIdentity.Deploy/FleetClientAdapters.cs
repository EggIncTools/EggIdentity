using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed class FleetClientEnvSource(FleetClient client) : IEnvSource {
    public Task<IReadOnlyList<EnvKeyInfo>> GetAsync(CancellationToken ct) => client.GetEnvAsync(client.AppName, ct);
}

public sealed class FleetClientRestartTrigger(FleetClient client) : IRestartTrigger {
    public Task<string?> RestartAsync(CancellationToken ct) => client.RestartAsync(client.AppName, ct);
}

public sealed class FleetClientStackEnvEditor(FleetClient client) : IStackEnvEditor {
    public Task<string?> ApplyAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken ct) =>
        client.PatchStackEnvAsync(client.AppName, changes, ct);
}
