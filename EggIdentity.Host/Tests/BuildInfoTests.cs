using System.Reflection;
using EggIdentity.Host;
using Xunit;

namespace EggIdentity.Host.Tests;

public class BuildInfoTests {
    [Fact]
    public void Build_ReadsGitShaFromEnv() {
        var info = BuildInfo.Build(key => key == "GIT_SHA" ? "abc123" : null, typeof(BuildInfoTests).Assembly);
        Assert.Equal("abc123", info.Sha256);
        Assert.Equal("EggIdentity", info.Name);
    }

    [Fact]
    public void Build_NoGitSha_EmptySha256() {
        var info = BuildInfo.Build(_ => null, typeof(BuildInfoTests).Assembly);
        Assert.Equal("", info.Sha256);
    }
}
