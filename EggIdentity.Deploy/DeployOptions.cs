using EggIdentity.Resilience;

namespace EggIdentity.Deploy;

public sealed record DeployOptions(string AgentUrl, string AppName) {
    public const string HttpClientName = "eggidentity-deploy-agent";
    public const string AgentUrlEnv = "DEPLOY_AGENT_URL";
    public static readonly TimeSpan MaxReconnectCeiling = TimeSpan.FromSeconds(60);

    public string? CallerName { get; init; }
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxReconnectDelay { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan CallTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan StreamIdleTimeout { get; init; } = TimeSpan.FromSeconds(45);

    public RetryOptions ReconnectRetry => new() {
        MaxAttempts = int.MaxValue,
        BaseDelay = ReconnectDelay,
        MaxDelay = MaxReconnectDelay < MaxReconnectCeiling ? MaxReconnectDelay : MaxReconnectCeiling,
    };

    public Uri BaseAddress => new(AgentUrl.EndsWith('/') ? AgentUrl : AgentUrl + "/", UriKind.Absolute);
}
