using EggIdentity.Contract;
using EggIdentity.Models;

namespace EggIdentity.Host.Tests;

public class ConsentRoutesTests {
    [Fact]
    public void ToResponse_MapsAllFields() {
        var decidedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var row = new CookieConsent {
            UserId = Guid.NewGuid(),
            Functional = true,
            Analytics = false,
            PolicyVersion = 3,
            DecidedAt = decidedAt,
            UpdatedAt = decidedAt,
        };

        var resp = ConsentRoutes.ToResponse(row);

        Assert.True(resp.Functional);
        Assert.False(resp.Analytics);
        Assert.Equal(3, resp.PolicyVersion);
        Assert.Equal(decidedAt, resp.DecidedAt);
    }

    [Fact]
    public void IsValid_RejectsNonPositivePolicyVersion() {
        Assert.False(ConsentRoutes.IsValid(new ConsentRequest { PolicyVersion = 0 }));
        Assert.False(ConsentRoutes.IsValid(new ConsentRequest { PolicyVersion = -1 }));
        Assert.True(ConsentRoutes.IsValid(new ConsentRequest { PolicyVersion = 1 }));
    }
}
