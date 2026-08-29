using EggIdentity.Host;
using Xunit;

namespace EggIdentity.Host.Tests;

public class SponsorConfigTests {
    private static readonly string[] AllVars = [
        "GITHUB_SPONSOR_PAT", "GITHUB_SPONSOR_TARGET", "GITHUB_SPONSOR_WEBHOOK_SECRET",
        "DISCORD_SPONSOR_BOT_TOKEN", "DISCORD_SPONSOR_GUILD_ID", "DISCORD_SPONSOR_ROLE_ID",
    ];

    private static void ClearAll() {
        foreach (var v in AllVars) Environment.SetEnvironmentVariable(v, null);
    }

    [Fact]
    public void FromEnvironment_AllRequiredVarsSet_ReturnsConfigWithDefaultTarget() {
        ClearAll();
        try {
            Environment.SetEnvironmentVariable("GITHUB_SPONSOR_PAT", "pat-1");
            Environment.SetEnvironmentVariable("GITHUB_SPONSOR_WEBHOOK_SECRET", "whsecret");
            Environment.SetEnvironmentVariable("DISCORD_SPONSOR_BOT_TOKEN", "bottoken");
            Environment.SetEnvironmentVariable("DISCORD_SPONSOR_GUILD_ID", "guild-1");
            Environment.SetEnvironmentVariable("DISCORD_SPONSOR_ROLE_ID", "role-1");

            var config = SponsorConfig.FromEnvironment();

            Assert.NotNull(config);
            Assert.Equal("pat-1", config!.GitHubPat);
            Assert.Equal("DavidArthurCole", config.GitHubTarget);
            Assert.Equal("whsecret", config.GitHubWebhookSecret);
            Assert.Equal("bottoken", config.DiscordBotToken);
            Assert.Equal("guild-1", config.DiscordGuildId);
            Assert.Equal("role-1", config.DiscordRoleId);
        } finally {
            ClearAll();
        }
    }

    [Fact]
    public void FromEnvironment_TargetOverridden_UsesOverride() {
        ClearAll();
        try {
            Environment.SetEnvironmentVariable("GITHUB_SPONSOR_PAT", "pat-1");
            Environment.SetEnvironmentVariable("GITHUB_SPONSOR_TARGET", "SomeOtherOrg");
            Environment.SetEnvironmentVariable("GITHUB_SPONSOR_WEBHOOK_SECRET", "whsecret");
            Environment.SetEnvironmentVariable("DISCORD_SPONSOR_BOT_TOKEN", "bottoken");
            Environment.SetEnvironmentVariable("DISCORD_SPONSOR_GUILD_ID", "guild-1");
            Environment.SetEnvironmentVariable("DISCORD_SPONSOR_ROLE_ID", "role-1");

            var config = SponsorConfig.FromEnvironment();

            Assert.Equal("SomeOtherOrg", config!.GitHubTarget);
        } finally {
            ClearAll();
        }
    }

    [Fact]
    public void FromEnvironment_MissingRequiredVar_ReturnsNull() {
        ClearAll();
        try {
            Environment.SetEnvironmentVariable("GITHUB_SPONSOR_PAT", "pat-1");

            var config = SponsorConfig.FromEnvironment();

            Assert.Null(config);
        } finally {
            ClearAll();
        }
    }
}
