using System.Text.Json;

namespace EggIdentity.Agent;

public sealed record ContainerInfo(
    string Id,
    string Name,
    string Image,
    string ImageId,
    IReadOnlyList<string> RepoDigests,
    IReadOnlyList<string> Env,
    IReadOnlyDictionary<string, string> Labels,
    bool Running,
    JsonElement Config,
    JsonElement HostConfig,
    JsonElement Networks) {
    public string? Revision => Labels.GetValueOrDefault(OciLabels.Revision);
    public string? Version => Labels.GetValueOrDefault(OciLabels.Version);
}

public sealed record ImageInfo(
    string Id,
    IReadOnlyList<string> RepoDigests,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<string> Env) {
    public string? Revision => Labels.GetValueOrDefault(OciLabels.Revision);
    public string? Version => Labels.GetValueOrDefault(OciLabels.Version);
}

public static class OciLabels {
    public const string Revision = "org.opencontainers.image.revision";
    public const string Version = "org.opencontainers.image.version";
}

public sealed record ContainerSpec(string Name, string Image, JsonElement Config, JsonElement HostConfig, JsonElement Networks) {
    public IReadOnlyList<string>? Cmd { get; init; }
    public IReadOnlyList<string>? Binds { get; init; }
    public bool AutoRemove { get; init; }
    public string? NetworkMode { get; init; }
}

public interface IDockerEngine {
    Task<ContainerInfo?> InspectContainerAsync(string name, CancellationToken ct);
    Task<ImageInfo?> InspectImageAsync(string reference, CancellationToken ct);
    Task PullImageAsync(string reference, IProgress<string>? progress, CancellationToken ct);
    Task RenameAsync(string name, string newName, CancellationToken ct);
    Task<string> CreateAsync(ContainerSpec spec, CancellationToken ct);
    Task StartAsync(string name, CancellationToken ct);
    Task StopAsync(string name, CancellationToken ct);
    Task RemoveAsync(string name, CancellationToken ct);
    Task RestartAsync(string name, CancellationToken ct);
    Task<string> LogsTailAsync(string name, int lines, CancellationToken ct);
}
