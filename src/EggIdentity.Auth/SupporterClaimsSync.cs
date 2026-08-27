using System.Security.Claims;
using EggIdentity.Client;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Auth;

public static class SupporterClaimsSync {
    public static Task OnValidated(ClaimsPrincipal principal, HttpContext ctx, CancellationToken ct) =>
        SyncAsync(principal, ctx, ct);

    public static void Stamp(ClaimsIdentity? identity, bool isSupporter) {
        if (identity is null) return;
        if (identity.FindFirst(SessionClaims.Supporter) is { } existing) identity.RemoveClaim(existing);
        identity.AddClaim(new Claim(SessionClaims.Supporter, isSupporter ? "true" : "false"));
    }

    private static async Task SyncAsync(ClaimsPrincipal principal, HttpContext ctx, CancellationToken ct) {
        var userId = principal.EggIdentityUserId();
        var identity = principal.Identity as ClaimsIdentity;
        if (userId is null || identity is null) return;

        var client = ctx.RequestServices.GetService<IdentityApiClient>();
        if (client is null) return;

        bool isSupporter;
        try {
            var status = await client.GetSupporterStatusAsync(userId.Value, ct);
            isSupporter = status.IsSupporter;
        } catch (Exception) {
            isSupporter = false;
        }

        Stamp(identity, isSupporter);
    }
}
