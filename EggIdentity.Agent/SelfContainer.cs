namespace EggIdentity.Agent;

public static class SelfContainer {
    public const string EnvKey = "AGENT_SELF_CONTAINER";

    public static string? Name() => Environment.GetEnvironmentVariable(EnvKey);
}
