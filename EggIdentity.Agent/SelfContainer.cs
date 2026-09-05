namespace EggIdentity.Agent;

public static class SelfContainer {
    public const string EnvKey = "AGENT_SELF_CONTAINER";

    public static bool IsSelf(ContainerInfo container) =>
        IsSelf(container, Environment.MachineName, Environment.GetEnvironmentVariable(EnvKey));

    public static bool IsSelf(ContainerInfo container, string? hostname, string? selfName) {
        ArgumentNullException.ThrowIfNull(container);
        var shortId = hostname?.ToLowerInvariant();
        if (DockerJson.IsAutoContainerId(shortId) && container.Id.StartsWith(shortId!, StringComparison.Ordinal)) return true;
        return !string.IsNullOrEmpty(selfName) && string.Equals(container.Name, selfName, StringComparison.Ordinal);
    }
}
