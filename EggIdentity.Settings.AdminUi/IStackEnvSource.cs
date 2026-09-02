namespace EggIdentity.Settings.AdminUi;

public interface IStackEnvSource {
    Task<IReadOnlyList<string>> GetStackKeysAsync(CancellationToken ct);
}

public interface IRestartTrigger {
    Task<string?> RestartAsync(CancellationToken ct);
}
