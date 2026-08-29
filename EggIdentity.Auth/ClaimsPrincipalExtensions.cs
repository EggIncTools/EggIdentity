using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EggIdentity.Contract;

namespace EggIdentity.Auth;

public static class ClaimsPrincipalExtensions {
    public static Guid? EggIdentityUserId(this ClaimsPrincipal principal) {
        var raw = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static UserRole EggIdentityRole(this ClaimsPrincipal principal) =>
        UserRoles.Parse(principal.FindFirstValue(SessionClaims.Role));

    public static bool IsAtLeast(this ClaimsPrincipal principal, UserRole need) =>
        UserRoles.IsAtLeast(principal.EggIdentityRole(), need);

    public static bool IsSupporter(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(SessionClaims.Supporter) == "true";
}
