using EggIdentity.Contract;
using EggIdentity.Models;

namespace EggIdentity.Host;

internal static class IdentityApiRoutes {
    private static readonly string[] PublicSegments = [
        "/auth", "/eggidentity-login.js", "/profile", "/avatars", "/webhooks", "/admin/api",
        "/privacy", "/terms",
    ];

    public static void UseBearerGate(WebApplication app, HostConfig config) {
        app.Use(async (ctx, next) => {
            if (IsPublic(ctx.Request.Path)) {
                await next();
                return;
            }
            if (ctx.Request.Headers.Authorization.ToString() != $"Bearer {config.ApiSecret}") {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync("unauthorized");
                return;
            }
            await next();
        });
    }

    private static bool IsPublic(PathString path) {
        if (path == "/") return true;
        foreach (var segment in PublicSegments) {
            if (path.StartsWithSegments(segment)) return true;
        }
        return false;
    }

    public static void Map(WebApplication app, HostConfig config) {
        app.MapPost("/identity/resolve", async (IdentityResolveRequest req, IdentityResolver resolver, HttpContext ctx, CancellationToken ct) => {
            var sourceIds = new Dictionary<string, string?>(req.SourceIds ?? [], StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(req.DiscordId)) sourceIds.TryAdd(IdentityWire.Discord, req.DiscordId);
            var result = await resolver.ResolveWithSourcesAsync(req.Provider, req.Subject, sourceIds, req.Username, req.Avatar, ct);
            if (req.Provider == "authentik" && !string.IsNullOrEmpty(req.Username))
                await IdentityReconcileService.TryReconcileAsync(ctx.RequestServices, result.UserId, req.Username, req.Subject, ct);
            return Results.Ok(new IdentityResolveResponse {
                UserId = result.UserId,
                Role = result.Role,
                DiscordId = result.DiscordId,
                IsNew = result.IsNew,
                MergedUserIds = [.. result.MergedUserIds],
            });
        });

        app.MapGet("/identity/merges", async (DateTimeOffset? since, int? limit, UserQueries users, CancellationToken ct) => {
            var merges = await users.ListMergesAsync(since, Math.Clamp(limit ?? 500, 1, 5000), ct);
            return Results.Ok(merges.Select(m => new UserMergeResponse {
                MergedUserId = m.MergedUserId,
                KeptUserId = m.KeptUserId,
                MergedAt = m.MergedAt,
            }));
        });

        app.MapGet("/identity/{userId:guid}", async (Guid userId, UserQueries users, CancellationToken ct) => {
            var user = await users.GetAsync(userId, ct);
            return user is null ? Results.NotFound() : Results.Ok(ToResponse(user));
        });

        if (config.SponsorConfig is not null) MapSponsorLookups(app);

        app.MapPost("/identity/revoke-session", async (RevokeSessionRequest req, RevocationStore store, CancellationToken ct) => {
            await store.RevokeAsync(req.Sid, ct);
            return Results.NoContent();
        });

        app.MapGet("/identity/sessions/{sid}/revoked", async (string sid, RevocationStore store, CancellationToken ct) =>
            Results.Ok(await store.IsRevokedAsync(sid, ct)));

        app.MapPost("/identity/merge", async (MergeUsersRequest req, IdentityResolver resolver, CancellationToken ct) => {
            var winner = await resolver.MergeAsync(req.KeepUserId, req.MergeUserId, ct);
            return Results.Ok(new { userId = winner });
        });

        app.MapGet("/identity/admin/users", async (UserQueries users, CancellationToken ct) => {
            var list = await users.ListAsync(ct);
            var providers = await users.ListProvidersAsync(ct);
            return Results.Ok(list.Select(u => ToResponse(u, providers.GetValueOrDefault(u.UserId) ?? [])));
        });

        app.MapPost("/identity/{userId:guid}/role", async (Guid userId, SetRoleRequest req, UserQueries users, CancellationToken ct) => {
            var ok = await users.SetRoleAsync(userId, UserRoles.Parse(req.Role), ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        app.MapPost("/identity/redeem", async (RedeemLoginCodeRequest req, LoginCodeStore codes, UserQueries users, CancellationToken ct) => {
            var redeemed = await codes.RedeemAsync(req.Code, ct);
            if (redeemed is null) return Results.NotFound();
            var user = await users.GetAsync(redeemed.UserId, ct);
            return user is null ? Results.NotFound() : Results.Ok(new RedeemLoginCodeResponse {
                UserId = user.UserId,
                DiscordId = user.DiscordId,
                Username = user.Username,
                Avatar = AvatarUrl.Canonical(user.UserId, user.Avatar),
                Role = user.Role,
                IsNew = redeemed.IsNew,
            });
        });
    }

    private static void MapSponsorLookups(WebApplication app) {
        app.MapGet("/identity/{userId:guid}/sponsor", async (Guid userId, GitHubSponsorStatusStore store, CancellationToken ct) => {
            var status = await store.GetAsync(userId, ct);
            return Results.Ok(new SponsorStatusResponse {
                IsSponsor = status?.IsSponsor ?? false,
                LastSyncedAt = status?.LastSyncedAt,
            });
        });

        app.MapGet("/identity/{userId:guid}/supporter", async (Guid userId, SupporterStatusService supporterStatus, CancellationToken ct) => {
            var isSupporter = await supporterStatus.IsSupporterAsync(userId, ct);
            return Results.Ok(new SupporterStatusResponse { IsSupporter = isSupporter });
        });
    }

    private static IdentityUserResponse ToResponse(User u, List<string>? providers = null) => new() {
        UserId = u.UserId,
        DiscordId = u.DiscordId,
        Username = u.Username,
        Avatar = AvatarUrl.Canonical(u.UserId, u.Avatar),
        Role = u.Role,
        Providers = providers ?? [],
        CreatedAt = u.CreatedAt,
        LastLoginAt = u.LastLoginAt,
    };
}
