using EggIdentity.Settings;

namespace EggIdentity.Fleet;

public static class FleetSettings {
    public const string PortainerApiUrl = "portainer.api_url";
    public const string PortainerApiKey = "portainer.api_key";
    public const string HookSecret = "deploy.hook_secret";
    public const string PullTimeout = "fleet.pull_timeout";
    public const string RedeployTimeout = "fleet.redeploy_timeout";
    public const string GitHubToken = "github.fleet_token";
    public const string CommitterName = "github.committer_name";
    public const string CommitterEmail = "github.committer_email";

    private const string Category = "Fleet";

    public static ISettingsProvider Provider { get; } = new StaticSettingsProvider([
        new SettingDescriptor(
            PortainerApiUrl, "PORTAINER_API_URL", "Portainer API URL", Category,
            SettingKind.Url, ApplyTier.RestartRequired, Sensitivity.Plain) {
            Description = "Base URL of Portainer, for example http://portainer:9000. Enables deploys, restarts, logs, drift and stack env edits.",
        },
        new SettingDescriptor(
            PortainerApiKey, "PORTAINER_API_KEY", "Portainer API key", Category,
            SettingKind.Secret, ApplyTier.RestartRequired, Sensitivity.Secret) {
            Description = "Needs stack read and update plus Docker access on each environment the suite runs in.",
        },
        new SettingDescriptor(
            HookSecret, "DEPLOY_HOOK_SECRET", "CI hook secret", Category,
            SettingKind.Secret, ApplyTier.Live, Sensitivity.Secret) {
            Description = "Bearer accepted on POST /hooks/image-pushed.",
        },
        new SettingDescriptor(
            PullTimeout, "FLEET_PULL_TIMEOUT", "Image pull timeout", Category,
            SettingKind.Duration, ApplyTier.Live, Sensitivity.Plain) { Default = "10m" },
        new SettingDescriptor(
            RedeployTimeout, "FLEET_REDEPLOY_TIMEOUT", "Redeploy timeout", Category,
            SettingKind.Duration, ApplyTier.Live, Sensitivity.Plain) {
            Default = "5m",
            Description = "Time for Portainer to bring the container up on the new image.",
        },
        new SettingDescriptor(
            GitHubToken, "GITHUB_FLEET_TOKEN", "GitHub fleet token", Category,
            SettingKind.Secret, ApplyTier.Live, Sensitivity.Secret) {
            Description = "Fine-grained token with contents read and write on the app repos. Used only to commit upstream pins.",
        },
        new SettingDescriptor(
            CommitterName, "GITHUB_COMMITTER_NAME", "Pin committer name", Category,
            SettingKind.Text, ApplyTier.Live, Sensitivity.Plain) {
            Default = GitCommitter.Default.Name,
            Description = "Name shown on upstream pin commits.",
        },
        new SettingDescriptor(
            CommitterEmail, "GITHUB_COMMITTER_EMAIL", "Pin committer email", Category,
            SettingKind.Text, ApplyTier.Live, Sensitivity.Plain) {
            Default = GitCommitter.Default.Email,
            Description = "Email on upstream pin commits. GitHub shows the avatar of the account that has this email verified, so use a bot account's email or its users.noreply.github.com address.",
        },
    ]);
}
