using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EggIdentity.Auth;
using EggIdentity.Contract;

namespace EggIdentity.Testing;

public sealed record FakeUser(
    Guid Id,
    UserRole Role = UserRole.Viewer,
    string? Name = "Test User",
    string? DiscordId = null,
    string? Avatar = null,
    bool Supporter = false) {
    public const string AuthenticationType = "Fake";

    public static FakeUser Viewer() => new(Guid.NewGuid());

    public static FakeUser Admin() => new(Guid.NewGuid(), UserRole.Admin);

    public ClaimsPrincipal Principal() {
        var claims = new List<Claim> {
            new(JwtRegisteredClaimNames.Sub, Id.ToString()),
            new(SessionClaims.Role, UserRoles.ToName(Role)),
            new(SessionClaims.Supporter, Supporter ? "true" : "false"),
        };
        if (Name is not null) claims.Add(new Claim(SessionClaims.Name, Name));
        if (DiscordId is not null) claims.Add(new Claim(SessionClaims.DiscordId, DiscordId));
        if (Avatar is not null) claims.Add(new Claim(SessionClaims.Avatar, Avatar));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType));
    }

    public CurrentUser Current() => Principal().ToCurrentUser();

    public ICurrentUser Accessor() => new FixedCurrentUser(Current());

    public static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private sealed class FixedCurrentUser(CurrentUser user) : ICurrentUser {
        public Task<CurrentUser> GetAsync() => Task.FromResult(user);
    }
}
