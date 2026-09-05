using EggIdentity.Settings;

namespace EggIdentity.Agent;

public static class AgentSettings {
    public const string DbConnection = "identity.db_connection";
    public const string Port = "agent.port";
    public const string WatchInterval = "agent.watch_interval";
    public const string PullTimeout = "agent.pull_timeout";
    public const string HookSecret = "deploy.hook_secret";
    public const string DockerHost = "agent.docker_host";
    public const string SelfContainer = "agent.self_container";
    public const string PortainerApiUrl = "portainer.api_url";
    public const string PortainerApiKey = "portainer.api_key";
    public const string PortainerStackId = "portainer.stack_id";
    public const string PortainerEndpointId = "portainer.endpoint_id";

    private const string Core = "Core";
    private const string Deploy = "Deploy";
    private const string Portainer = "Portainer";

    public static ISettingsProvider Provider { get; } = new StaticSettingsProvider([
        new SettingDescriptor(
            DbConnection, "IDENTITY_DB_CONNECTION", "Postgres connection string", Core,
            SettingKind.Secret, ApplyTier.Bootstrap, Sensitivity.Secret) {
            Description = "The deploy.apps collection and every other stored setting live here.",
            Required = true,
        },
        new SettingDescriptor(
            Port, "AGENT_PORT", "Listen port", Core,
            SettingKind.Number, ApplyTier.Bootstrap, Sensitivity.Plain) { Default = "7777" },
        new SettingDescriptor(
            DockerHost, "DOCKER_HOST", "Docker engine endpoint", Core,
            SettingKind.Text, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "unix:// socket path or tcp:// address. Defaults to /var/run/docker.sock.",
        },
        new SettingDescriptor(
            SelfContainer, "AGENT_SELF_CONTAINER", "Own container name", Core,
            SettingKind.Text, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "Container name of this agent instance when the hostname is not the container id.",
        },
        new SettingDescriptor(
            WatchInterval, "AGENT_WATCH_INTERVAL", "Registry poll interval", Deploy,
            SettingKind.Duration, ApplyTier.Live, Sensitivity.Plain) {
            Default = "30s",
            Description = "Fallback manifest check cadence when the CI hook is lost.",
        },
        new SettingDescriptor(
            PullTimeout, "AGENT_PULL_TIMEOUT", "Image pull timeout", Deploy,
            SettingKind.Duration, ApplyTier.Live, Sensitivity.Plain) { Default = "10m" },
        new SettingDescriptor(
            HookSecret, "DEPLOY_HOOK_SECRET", "CI hook secret", Deploy,
            SettingKind.Secret, ApplyTier.RestartRequired, Sensitivity.Secret) {
            Description = "Bearer accepted on POST /hooks/image-pushed.",
        },
        new SettingDescriptor(
            PortainerApiUrl, "PORTAINER_API_URL", "Portainer API URL", Portainer,
            SettingKind.Url, ApplyTier.RestartRequired, Sensitivity.Plain),
        new SettingDescriptor(
            PortainerApiKey, "PORTAINER_API_KEY", "Portainer API key", Portainer,
            SettingKind.Secret, ApplyTier.RestartRequired, Sensitivity.Secret),
        new SettingDescriptor(
            PortainerStackId, "PORTAINER_STACK_ID", "Portainer stack id", Portainer,
            SettingKind.Number, ApplyTier.RestartRequired, Sensitivity.Plain),
        new SettingDescriptor(
            PortainerEndpointId, "PORTAINER_ENDPOINT_ID", "Portainer endpoint id", Portainer,
            SettingKind.Number, ApplyTier.RestartRequired, Sensitivity.Plain),
    ]);
}
