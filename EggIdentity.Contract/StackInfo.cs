namespace EggIdentity.Contract;

public sealed record StackInfo(
    string Name,
    int StackId,
    int EndpointId,
    string? PortainerName,
    bool GitBacked,
    string? RepositoryUrl,
    string? ReferenceName,
    bool WebhookArmed,
    bool ForceUpdate,
    string? Refusal) {
    public bool Ready => Refusal is null;
}
