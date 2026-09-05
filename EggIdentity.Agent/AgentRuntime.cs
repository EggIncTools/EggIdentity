namespace EggIdentity.Agent;

internal sealed record AgentRuntime(
    DeployService Service,
    DeployEventRing Events,
    IDockerEngine Engine,
    PortainerConfig? Portainer,
    string? HookSecret);
