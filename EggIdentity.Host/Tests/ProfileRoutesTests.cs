using EggIdentity.Host;
using Xunit;

namespace EggIdentity.Host.Tests;

public class ProfileRoutesTests {
    private static EggIdentity.Models.Identity Make(string provider, string subject) => new() {
        UserId = Guid.NewGuid(),
        Provider = provider,
        Subject = subject,
        LinkedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public void FilterIdentitiesForDisplay_OnlyAuthentikRow_KeepsIt() {
        var identities = new[] { Make("authentik", "sub-1") };

        var result = ProfileRoutes.FilterIdentitiesForDisplay(identities);

        Assert.Single(result);
        Assert.Equal("authentik", result[0].Provider);
    }

    [Fact]
    public void FilterIdentitiesForDisplay_AuthentikPlusSourceRows_HidesAuthentikRow() {
        var identities = new[] { Make("authentik", "sub-1"), Make("discord", "d-1"), Make("google", "g-1") };

        var result = ProfileRoutes.FilterIdentitiesForDisplay(identities);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, i => i.Provider == "authentik");
    }

    [Fact]
    public void FilterIdentitiesForDisplay_LegacyDiscordOnly_KeepsIt() {
        var identities = new[] { Make("discord", "d-1") };

        var result = ProfileRoutes.FilterIdentitiesForDisplay(identities);

        Assert.Single(result);
        Assert.Equal("discord", result[0].Provider);
    }
}
