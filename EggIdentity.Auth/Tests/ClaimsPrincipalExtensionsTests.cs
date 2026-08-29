using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EggIdentity.Auth;
using EggIdentity.Contract;
using Xunit;

namespace EggIdentity.Auth.Tests;

public class ClaimsPrincipalExtensionsTests {
    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));

    [Fact]
    public void EggIdentityRole_ParsesRoleClaim() {
        var principal = Principal(new Claim(SessionClaims.Role, "admin"));

        Assert.Equal(UserRole.Admin, principal.EggIdentityRole());
    }

    [Fact]
    public void EggIdentityRole_MissingClaim_DefaultsToViewer() {
        Assert.Equal(UserRole.Viewer, Principal().EggIdentityRole());
    }

    [Fact]
    public void IsAtLeast_AdminMeetsContributor() {
        var principal = Principal(new Claim(SessionClaims.Role, "admin"));

        Assert.True(principal.IsAtLeast(UserRole.Contributor));
        Assert.True(principal.IsAtLeast(UserRole.Admin));
    }

    [Fact]
    public void IsAtLeast_ViewerFailsContributor() {
        var principal = Principal(new Claim(SessionClaims.Role, "viewer"));

        Assert.False(principal.IsAtLeast(UserRole.Contributor));
    }

    [Fact]
    public void EggIdentityUserId_ParsesSubClaim() {
        var id = Guid.NewGuid();
        var principal = Principal(new Claim(JwtRegisteredClaimNames.Sub, id.ToString()));

        Assert.Equal(id, principal.EggIdentityUserId());
    }

    [Fact]
    public void EggIdentityUserId_MissingClaim_ReturnsNull() {
        Assert.Null(Principal().EggIdentityUserId());
    }
}
