namespace EggIdentity.Host;

public sealed record SponsorConfig(
    string GitHubPat,
    string GitHubTarget,
    string GitHubWebhookSecret,
    string DiscordBotToken,
    string DiscordGuildId,
    string DiscordRoleId) {

    public static SponsorConfig? FromEnvironment() {
        var pat = Environment.GetEnvironmentVariable("GITHUB_SPONSOR_PAT");
        var webhookSecret = Environment.GetEnvironmentVariable("GITHUB_SPONSOR_WEBHOOK_SECRET");
        var botToken = Environment.GetEnvironmentVariable("DISCORD_SPONSOR_BOT_TOKEN");
        var guildId = Environment.GetEnvironmentVariable("DISCORD_SPONSOR_GUILD_ID");
        var roleId = Environment.GetEnvironmentVariable("DISCORD_SPONSOR_ROLE_ID");
        if (string.IsNullOrEmpty(pat) || string.IsNullOrEmpty(webhookSecret) || string.IsNullOrEmpty(botToken)
            || string.IsNullOrEmpty(guildId) || string.IsNullOrEmpty(roleId)) {
            return null;
        }

        var target = Environment.GetEnvironmentVariable("GITHUB_SPONSOR_TARGET");
        return new SponsorConfig(
            GitHubPat: pat,
            GitHubTarget: string.IsNullOrEmpty(target) ? "DavidArthurCole" : target,
            GitHubWebhookSecret: webhookSecret,
            DiscordBotToken: botToken,
            DiscordGuildId: guildId,
            DiscordRoleId: roleId);
    }
}
