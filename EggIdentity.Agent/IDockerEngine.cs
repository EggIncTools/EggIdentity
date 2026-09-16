namespace EggIdentity.Agent;

public sealed record ContainerInfo(
    string Id,
    string Name,
    string Image,
    string ImageId,
    IReadOnlyList<string> RepoDigests,
    IReadOnlyList<string> Env,
    IReadOnlyDictionary<string, string> Labels,
    bool Running) {
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

public interface IDockerEngine {
    Task<ContainerInfo?> InspectContainerAsync(string name, CancellationToken ct);
    Task<ImageInfo?> InspectImageAsync(string reference, CancellationToken ct);
    Task PullImageAsync(string reference, IProgress<string>? progress, CancellationToken ct);
    Task RestartAsync(string name, CancellationToken ct);
    Task<string> LogsTailAsync(string name, int lines, CancellationToken ct);
}
