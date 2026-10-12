using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.Settings;

namespace EggIdentity.Host.Tests;

public class SponsorConfigTests {
    private static readonly DiscordRegistrationResponse Registration = new() { Token = "bottoken", GuildId = "guild-1" };

    private static SettingsSnapshot Snapshot(params (string Key, string Value)[] values) {
        var registry = SettingsRegistry.Compose([HostSettings.Provider, SessionSettings.Provider], []);
        return new SettingsSnapshot(registry, values.ToDictionary(v => v.Key, v => (string?)v.Value), environment: _ => null);
    }

    private static readonly (string, string)[] Required = [
        (HostSettings.SponsorPat, "pat-1"),
        (HostSettings.SponsorWebhookSecret, "whsecret"),
        (HostSettings.SponsorRoleId, "role-1"),
    ];

    [Fact]
    public void From_AllRequiredSet_TakesDiscordFromRegistrationWithDefaultTarget() {
        var config = SponsorConfig.From(Snapshot(Required), Registration);

        Assert.NotNull(config);
        Assert.Equal("pat-1", config.GitHubPat);
        Assert.Equal(SponsorConfig.DefaultTarget, config.GitHubTarget);
        Assert.Equal("whsecret", config.GitHubWebhookSecret);
        Assert.Equal("bottoken", config.DiscordBotToken);
        Assert.Equal("guild-1", config.DiscordGuildId);
        Assert.Equal("role-1", config.DiscordRoleId);
    }

    [Fact]
    public void From_TargetOverridden_UsesOverride() {
        var config = SponsorConfig.From(Snapshot([.. Required, (HostSettings.SponsorTarget, "SomeOtherOrg")]), Registration);

        Assert.NotNull(config);
        Assert.Equal("SomeOtherOrg", config.GitHubTarget);
    }

    [Fact]
    public void From_MissingRequiredSetting_ReturnsNull() =>
        Assert.Null(SponsorConfig.From(Snapshot((HostSettings.SponsorPat, "pat-1")), Registration));

    [Fact]
    public void From_NoRegistration_ReturnsNull() =>
        Assert.Null(SponsorConfig.From(Snapshot(Required), null));
}
