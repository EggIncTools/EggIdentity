namespace EggIdentity.Bot.Tests;

public class BotConfigTests {
    [Fact]
    public void Defaults_GlobalCommandsAndGuildMirrorFalse_SupporterRoleEmpty() {
        var cfg = new BotConfig();

        Assert.False(cfg.GlobalCommands);
        Assert.False(cfg.GuildCommandMirror);
        Assert.Equal("", cfg.SupporterRoleId);
    }
}
