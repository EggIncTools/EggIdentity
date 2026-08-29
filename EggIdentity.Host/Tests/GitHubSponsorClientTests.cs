using EggIdentity.Host;
using Xunit;

namespace EggIdentity.Host.Tests;

public class GitHubSponsorClientTests {
    [Fact]
    public void ParseLogin_ValidJson_ReturnsLogin() {
        var json = """{"login":"octocat","id":12345}""";

        Assert.Equal("octocat", GitHubSponsorClient.ParseLogin(json));
    }

    [Fact]
    public void ParseLogin_MissingField_ReturnsNull() {
        Assert.Null(GitHubSponsorClient.ParseLogin("{\"id\":1}"));
    }

    [Fact]
    public void ParseIsSponsoredBy_True_ReturnsTrue() {
        var json = """{"data":{"user":{"isSponsoredBy":true}}}""";

        Assert.True(GitHubSponsorClient.ParseIsSponsoredBy(json));
    }

    [Fact]
    public void ParseIsSponsoredBy_False_ReturnsFalse() {
        var json = """{"data":{"user":{"isSponsoredBy":false}}}""";

        Assert.False(GitHubSponsorClient.ParseIsSponsoredBy(json));
    }
}
