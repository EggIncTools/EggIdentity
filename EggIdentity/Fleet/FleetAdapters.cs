using EggIdentity.Settings;

namespace EggIdentity.Fleet;

public sealed class FleetEnvSource(DeployService service, string app) : IEnvSource {
    public async Task<IReadOnlyList<EnvKeyInfo>> GetAsync(CancellationToken ct) {
        var (env, refusal) = await service.EnvAsync(app, ct);
        return env ?? throw new InvalidOperationException(refusal);
    }
}

public sealed class FleetRestartTrigger(DeployService service, string app) : IRestartTrigger {
    public Task<string?> RestartAsync(CancellationToken ct) => service.RestartAsync(app, ct);
}

public sealed class FleetStackEnvEditor(DeployService service, string app) : IStackEnvEditor {
    public Task<string?> ApplyAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken ct) =>
        service.PatchEnvAsync(app, changes, ct);
}
