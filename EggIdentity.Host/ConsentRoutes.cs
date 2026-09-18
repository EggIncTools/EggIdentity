using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.Models;

namespace EggIdentity.Host;

public static class ConsentRoutes {
    public static void Map(WebApplication app, SessionCookieOptions sessionOptions, RevocationStore revocations, ConsentService consents) {
        Func<string, CancellationToken, Task<bool>> isRevoked = revocations.IsRevokedAsync;
        var routes = app.MapGroup("/profile");

        routes.MapGet("/consent", async (HttpContext ctx) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, isRevoked, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();

            var row = await consents.GetAsync(userId.Value, ctx.RequestAborted);
            return row is null ? Results.NotFound() : Results.Ok(ToResponse(row));
        });

        routes.MapPut("/consent", async (HttpContext ctx, ConsentRequest req) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, isRevoked, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();
            if (!IsValid(req)) return Results.BadRequest("policyVersion must be positive");

            await consents.SetAsync(userId.Value, req.Functional, req.Analytics, req.PolicyVersion, req.DecidedAt, ctx.RequestAborted);
            return Results.NoContent();
        });
    }

    public static bool IsValid(ConsentRequest req) => req.PolicyVersion > 0;

    public static ConsentResponse ToResponse(CookieConsent row) => new() {
        Functional = row.Functional,
        Analytics = row.Analytics,
        PolicyVersion = row.PolicyVersion,
        DecidedAt = row.DecidedAt,
    };
}
