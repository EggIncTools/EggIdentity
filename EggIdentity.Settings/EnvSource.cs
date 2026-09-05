namespace EggIdentity.Settings;

public enum EnvOrigin {
    ServiceEnvironment,
    EnvFile,
    StackVariable,
    Image,
    Runtime
}

public sealed record EnvKeyInfo(string Name, EnvOrigin Origin) {
    public bool Masked { get; init; }
    public string? Value { get; init; }
    public bool Referenced { get; init; } = true;

    public bool IsPresentInContainer => Origin != EnvOrigin.StackVariable;
}

public interface IEnvSource {
    Task<IReadOnlyList<EnvKeyInfo>> GetAsync(CancellationToken ct);
}

public interface IRestartTrigger {
    Task<string?> RestartAsync(CancellationToken ct);
}

public interface IStackEnvEditor {
    Task<string?> ApplyAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken ct);
}
