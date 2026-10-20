using EggIdentity.Auth;
using EggIdentity.Contract;

namespace EggIdentity.Host;

public static class EidRoutes {
    public static void Map(WebApplication app, SessionCookieOptions sessionOptions, RevocationStore revocations, ProfileService profiles, UserQueries users, EidStore eids) {
        Func<string, CancellationToken, Task<bool>> isRevoked = revocations.IsRevokedAsync;
        Task<Guid?> UserAsync(HttpContext ctx) {
            return ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, isRevoked, ctx.RequestAborted);
        }

        var routes = app.MapGroup("/profile");

        routes.MapGet("/game", async (HttpContext ctx) => {
            if (await UserAsync(ctx) is not { } userId) return Results.Unauthorized();
            var user = await users.GetAsync(userId, ctx.RequestAborted);
            if (user is null) return Results.NotFound();
            var list = await eids.ListAsync(userId, ctx.RequestAborted);
            return Results.Ok(new ProfileGame { ActiveEid = user.ActiveEid, ActiveEidLabel = user.ActiveEidLabel, Eids = list });
        });

        routes.MapPost("/game", async (HttpContext ctx, ProfileGame req) => {
            if (await UserAsync(ctx) is not { } userId) return Results.Unauthorized();
            var eid = string.IsNullOrWhiteSpace(req.ActiveEid) ? null : req.ActiveEid.Trim();
            var label = EidRules.NormalizeLabel(req.ActiveEidLabel);
            if (eid?.Length > ProfileGame.MaxEidLength || label?.Length > ProfileGame.MaxLabelLength)
                return Results.BadRequest("activeEid or activeEidLabel too long");
            if (eid is not null && !await eids.EnsureAsync(userId, eid, label, ctx.RequestAborted))
                return Results.BadRequest("activeEid is not a valid EID or the account list is full");
            await profiles.SetGameAsync(userId, eid, eid is null ? null : label, ctx.RequestAborted);
            return Results.NoContent();
        });

        routes.MapPost("/eids", async (HttpContext ctx, List<EidUpsert> req) =>
            await UserAsync(ctx) is { } userId ? Results.Ok(await eids.UpsertAsync(userId, req, ctx.RequestAborted)) : Results.Unauthorized());

        routes.MapPost("/eids/order", async (HttpContext ctx, List<string> req) => {
            if (await UserAsync(ctx) is not { } userId) return Results.Unauthorized();
            var result = await eids.ReorderAsync(userId, req, ctx.RequestAborted);
            return result == EidReorderResult.Reordered ? Results.NoContent() : Results.Conflict("order must be a permutation of the current list");
        });

        routes.MapPost("/eids/{eid}/snapshot", async (HttpContext ctx, string eid, EidSnapshot req) => {
            if (await UserAsync(ctx) is not { } userId) return Results.Unauthorized();
            return await eids.UpdateSnapshotAsync(userId, eid, req, ctx.RequestAborted) ? Results.NoContent() : Results.NotFound();
        });

        routes.MapPost("/eids/{eid}/label", async (HttpContext ctx, string eid, EidLabelRequest req) => {
            if (await UserAsync(ctx) is not { } userId) return Results.Unauthorized();
            if (EidRules.NormalizeLabel(req.Label)?.Length > EidRules.MaxLabelLength) return Results.BadRequest("label too long");
            return await eids.RenameAsync(userId, eid, req.Label, ctx.RequestAborted) ? Results.NoContent() : Results.NotFound();
        });

        routes.MapDelete("/eids/{eid}", async (HttpContext ctx, string eid) => {
            if (await UserAsync(ctx) is not { } userId) return Results.Unauthorized();
            return await eids.RemoveAsync(userId, eid, ctx.RequestAborted) ? Results.NoContent() : Results.NotFound();
        });
    }
}
