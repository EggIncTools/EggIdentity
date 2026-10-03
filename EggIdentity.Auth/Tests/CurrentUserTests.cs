using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.Auth.Tests;

public class CurrentUserTests {
    private static readonly Guid Id = Guid.NewGuid();

    private static ClaimsPrincipal Admin() => new(new ClaimsIdentity([
        new Claim(JwtRegisteredClaimNames.Sub, Id.ToString()),
        new Claim(SessionClaims.Role, "admin"),
    ], "test"));

    private sealed class UnsetProvider : AuthenticationStateProvider {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            throw new InvalidOperationException("GetAuthenticationStateAsync was called before SetAuthenticationState.");
    }

    private sealed class FixedProvider(ClaimsPrincipal user) : AuthenticationStateProvider {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
    }

    private static IHttpContextAccessor Http(ClaimsPrincipal user) => new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } };

    [Fact]
    public async Task UnsetAuthStateFallsBackToHttpContextInsteadOfThrowing() {
        using var current = new CircuitCurrentUser(new UnsetProvider(), Http(Admin()));

        Assert.Equal(Id, current.Current.Id);
        Assert.True((await current.GetAsync()).IsAtLeast(UserRole.Admin));
    }

    [Fact]
    public async Task UnsetAuthStateWithoutHttpContextIsAnonymous() {
        using var current = new CircuitCurrentUser(new UnsetProvider());

        Assert.Same(CurrentUser.Anonymous, current.Current);
        Assert.Same(CurrentUser.Anonymous, await current.GetAsync());
    }

    [Fact]
    public async Task SetAuthStateWins() {
        using var current = new CircuitCurrentUser(new FixedProvider(Admin()), Http(new ClaimsPrincipal()));

        Assert.Equal(Id, current.Current.Id);
        Assert.Equal(Id, (await current.GetAsync()).Id);
    }
}
