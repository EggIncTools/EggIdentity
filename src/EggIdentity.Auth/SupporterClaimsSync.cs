using System.Security.Claims;
using EggIdentity.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Auth;

public static class SupporterClaimsSync {
    public static Task OnValidated(ClaimsPrincipal principal, HttpContext ctx, CancellationToken ct) =>
        SyncAsync(principal, ctx, ct, idClaimType: null);

    public static Func<ClaimsPrincipal, HttpContext, CancellationToken, Task> Create(string idClaimType) =>
        (principal, ctx, ct) => SyncAsync(principal, ctx, ct, idClaimType);

    public static void Stamp(ClaimsIdentity? identity, bool isSupporter) {
        if (identity is null) return;
        if (identity.FindFirst(SessionClaims.Supporter) is { } existing) identity.RemoveClaim(existing);
        identity.AddClaim(new Claim(SessionClaims.Supporter, isSupporter ? "true" : "false"));
    }

    private static async Task SyncAsync(ClaimsPrincipal principal, HttpContext ctx, CancellationToken ct, string? idClaimType) {
        if (principal.Identity is not ClaimsIdentity claimsIdentity) return;
        if (ResolveUserId(principal, idClaimType) is not Guid userId) return;

        var client = ctx.RequestServices.GetService<IdentityApiClient>();
        if (client is null) return;

        bool isSupporter;
        try {
            var status = await client.GetSupporterStatusAsync(userId, ct);
            isSupporter = status.IsSupporter;
        } catch (Exception) {
            isSupporter = false;
        }

        Stamp(claimsIdentity, isSupporter);
    }

    private static Guid? ResolveUserId(ClaimsPrincipal principal, string? idClaimType) {
        if (idClaimType is not null && Guid.TryParse(principal.FindFirstValue(idClaimType), out var claimId)) return claimId;
        return principal.EggIdentityUserId();
    }
}
