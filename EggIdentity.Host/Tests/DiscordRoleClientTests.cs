using EggIdentity.Host;
using Xunit;

namespace EggIdentity.Host.Tests;

public class DiscordRoleClientTests {
    [Fact]
    public void BuildRoleUrl_ComposesExpectedPath() {
        var url = DiscordRoleClient.BuildRoleUrl("111", "222", "333");

        Assert.Equal("https://discord.com/api/v10/guilds/111/members/222/roles/333", url);
    }
}
