using EggIdentity.Bot;
using Xunit;

namespace EggIdentity.Bot.Tests;

public class ThreadKindsTests {
    [Fact]
    public void ToName_MatchesEnumName() {
        Assert.Equal("GithubFeed", ThreadKinds.ToName(ThreadKind.GithubFeed));
        Assert.Equal("DeployNotifications", ThreadKinds.ToName(ThreadKind.DeployNotifications));
    }
}
