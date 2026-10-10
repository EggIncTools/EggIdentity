using System.Security.Claims;
using EggIdentity.Contract;

namespace EggIdentity.Auth;

public sealed record CurrentUser(
    Guid? Id,
    string? DiscordId,
    string? Name,
    string? Avatar,
    UserRole Role,
    bool IsSupporter,
    bool IsAuthenticated) {
    public static CurrentUser Anonymous { get; } = new(null, null, null, null, UserRole.Viewer, false, false);

    public bool IsAtLeast(UserRole need) => IsAuthenticated && UserRoles.IsAtLeast(Role, need);

    public string? AvatarUrl(string? identityHost) {
        if (string.IsNullOrEmpty(Avatar) || !Avatar.StartsWith('/') || Avatar.StartsWith("//", StringComparison.Ordinal)) return Avatar;
        return string.IsNullOrWhiteSpace(identityHost) ? Avatar : identityHost.TrimEnd('/') + Avatar;
    }

    public static CurrentUser From(ClaimsPrincipal? principal) {
        if (principal?.Identity?.IsAuthenticated != true) return Anonymous;
        return new CurrentUser(
            principal.EggIdentityUserId(),
            principal.FindFirstValue(SessionClaims.DiscordId),
            principal.FindFirstValue(SessionClaims.Name),
            principal.FindFirstValue(SessionClaims.Avatar),
            principal.EggIdentityRole(),
            principal.IsSupporter(),
            true);
    }
}
