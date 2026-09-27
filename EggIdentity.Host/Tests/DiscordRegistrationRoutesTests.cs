using EggIdentity.Deploy;

namespace EggIdentity.Host.Tests;

public class DiscordRegistrationRoutesTests {
    private static readonly SuiteApp Own = new() {
        Name = "egginctools",
        DiscordBot = SuiteApp.DiscordBotOwn,
        DiscordToken = "token",
        DiscordAppId = "11",
        DiscordGuildId = "22",
        DiscordDashboardChannelId = "33",
    };

    [Fact]
    public void OwnBotRow_MapsEveryRegistrationField() {
        var registration = DiscordRegistrationRoutes.ToResponse(Own);

        Assert.NotNull(registration);
        Assert.Equal("token", registration.Token);
        Assert.Equal("11", registration.AppId);
        Assert.Equal("22", registration.GuildId);
        Assert.Equal("33", registration.DashboardChannelId);
    }

    [Fact]
    public void NonOwnDisabledOrIncompleteRows_HaveNoRegistration() {
        Assert.Null(DiscordRegistrationRoutes.ToResponse(null));
        Assert.Null(DiscordRegistrationRoutes.ToResponse(Own with { DiscordBot = SuiteApp.DiscordBotSuite }));
        Assert.Null(DiscordRegistrationRoutes.ToResponse(Own with { DiscordBot = SuiteApp.DiscordBotNone }));
        Assert.Null(DiscordRegistrationRoutes.ToResponse(Own with { Enabled = false }));
        Assert.Null(DiscordRegistrationRoutes.ToResponse(Own with { DiscordToken = null }));
        Assert.Null(DiscordRegistrationRoutes.ToResponse(Own with { DiscordGuildId = "" }));
    }
}
