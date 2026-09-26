using EggIdentity.Auth;
using EggIdentity.Contract;

namespace EggIdentity.Host;

public static class ProfileRoutes {
    public static void Map(
        WebApplication app, SessionCookieOptions sessionOptions, string avatarStorageDir,
        RevocationStore revocations, ProfileService profiles, UserQueries users, IdentityReconciler? reconciler = null) {
        Func<string, CancellationToken, Task<bool>> isRevoked = revocations.IsRevokedAsync;

        var profileRoutes = app.MapGroup("/profile");

        profileRoutes.MapGet("/me", async (HttpContext ctx) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, isRevoked, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();

            var user = await users.GetAsync(userId.Value, ctx.RequestAborted);
            if (user is null) return Results.NotFound();
            var identities = await profiles.ListIdentitiesAsync(userId.Value, ctx.RequestAborted);

            return Results.Ok(new ProfileResponse {
                UserId = user.UserId,
                Username = user.Username,
                Avatar = AvatarUrl.Canonical(user.UserId, user.Avatar),
                AvatarIsCustom = user.AvatarIsCustom,
                Identities = [.. FilterIdentitiesForDisplay(identities)
                    .Select(i => new ProfileIdentityResponse {
                        Provider = i.Provider,
                        Subject = i.Subject,
                        Username = i.Username,
                        Avatar = i.Avatar,
                        LinkedAt = i.LinkedAt,
                    })],
                Timezone = user.Timezone,
                Language = user.Language,
                Theme = user.Theme,
            });
        });

        profileRoutes.MapPost("/preferences", async (HttpContext ctx, ProfilePreferencesRequest req) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, isRevoked, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();

            await profiles.SetPreferencesAsync(userId.Value, req.Timezone, req.Language, req.Theme, ctx.RequestAborted);
            return Results.NoContent();
        });

        profileRoutes.MapPost("/identities/{provider}/{subject}/unlink", async (HttpContext ctx, string provider, string subject) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, isRevoked, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();

            var result = await profiles.UnlinkAsync(userId.Value, provider, subject, ctx.RequestAborted);
            if (result == UnlinkResult.Unlinked && reconciler is not null) {
                try {
                    await reconciler.UnlinkAsync(userId.Value, provider, subject, ctx.RequestAborted);
                } catch (Exception exc) when (exc is not OperationCanceledException) {
                    Console.Error.WriteLine($"authentik disconnect failed for {userId.Value} {provider}: {exc.Message}");
                }
            }
            return result switch {
                UnlinkResult.Unlinked => Results.NoContent(),
                UnlinkResult.LastIdentity => Results.BadRequest("cannot unlink the last identity on an account"),
                _ => Results.NotFound(),
            };
        });

        profileRoutes.MapPost("/identities/sync", async (HttpContext ctx) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, isRevoked, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();
            if (reconciler is null) return Results.NotFound();

            var result = await reconciler.ReconcileAsync(userId.Value, ctx.RequestAborted);
            if (!result.Applied) return Results.Conflict("authentik user not found for this account");
            var stillExists = await users.GetAsync(userId.Value, ctx.RequestAborted) is not null;
            return stillExists ? Results.NoContent() : Results.Unauthorized();
        });

        profileRoutes.MapPost("/avatar", async (HttpContext ctx) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, isRevoked, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();
            if (!ctx.Request.HasFormContentType) return Results.BadRequest("expected multipart/form-data");

            var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
            var file = form.Files["file"];
            if (file is null) return Results.BadRequest("missing file");

            await using var stream = file.OpenReadStream();
            var url = await AvatarStore.SaveAsync(avatarStorageDir, userId.Value, stream, file.ContentType, ctx.RequestAborted);
            if (url is null) return Results.BadRequest("unsupported content-type or file too large");

            await profiles.SetCustomAvatarAsync(userId.Value, url, ctx.RequestAborted);
            return Results.NoContent();
        });

        profileRoutes.MapPost("/avatar/select", async (HttpContext ctx, AvatarSelectRequest req) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, isRevoked, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();

            var selected = await profiles.SelectIdentityAvatarAsync(userId.Value, req.Provider, req.Subject, ctx.RequestAborted);
            return selected ? Results.NoContent() : Results.NotFound();
        });

        AvatarRoutes.Map(app, avatarStorageDir, users);
    }

    public static IReadOnlyList<Models.Identity> FilterIdentitiesForDisplay(IReadOnlyList<Models.Identity> identities) {
        var hasSourceIdentity = identities.Any(i => i.Provider != "authentik");
        return [.. identities.Where(i => i.Provider != "authentik" || !hasSourceIdentity)];
    }
}
