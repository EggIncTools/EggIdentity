using System.Text;
using System.Text.Json;
using EggIdentity.Contract;
using EggIdentity.Models;

namespace EggIdentity.Host;

internal static class SponsorRoutes {
    public static void Map(WebApplication app, HostConfig config, SponsorSyncService sponsorSync) {
        var sessionOptions = config.SessionOptions!;
        var sponsorStore = app.Services.GetRequiredService<GitHubSponsorStatusStore>();
        var revocations = app.Services.GetRequiredService<RevocationStore>();

        app.MapPost("/profile/sponsor/refresh", async (HttpContext ctx) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, revocations.IsRevokedAsync, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();

            var existing = await sponsorStore.GetAsync(userId.Value, ctx.RequestAborted);
            if (Program.ShouldThrottleSponsorRefresh(existing?.LastSyncedAt, DateTimeOffset.UtcNow)) {
                ctx.Response.Headers.RetryAfter = "30";
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }

            GitHubSponsorStatus? status;
            try {
                status = await sponsorSync.SyncAsync(userId.Value, ctx.RequestAborted);
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                Console.Error.WriteLine($"sponsor refresh failed for {userId}: {exc.Message}");
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
            if (status is null) return Results.BadRequest("no linked github account");

            return Results.Ok(new SponsorStatusResponse {
                IsSponsor = status.IsSponsor,
                LastSyncedAt = status.LastSyncedAt,
            });
        });

        app.MapPost("/profile/supporter/refresh", async (HttpContext ctx) => {
            var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, revocations.IsRevokedAsync, ctx.RequestAborted);
            if (userId is null) return Results.Unauthorized();

            var supporterStatus = app.Services.GetRequiredService<SupporterStatusService>();
            bool? refreshed;
            try {
                refreshed = await supporterStatus.RefreshAsync(userId.Value, ctx.RequestAborted);
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                Console.Error.WriteLine($"supporter refresh failed for {userId}: {exc.Message}");
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
            if (refreshed is null) {
                ctx.Response.Headers.RetryAfter = "30";
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            }

            return Results.Ok(new SupporterStatusResponse { IsSupporter = refreshed.Value });
        });

        Func<HttpContext, Task<IResult>> webhook = ctx => Webhook(ctx, config, sponsorSync);
        app.MapPost("/webhooks/github/sponsorship", webhook);
    }

    private static async Task<IResult> Webhook(HttpContext ctx, HostConfig config, SponsorSyncService sponsorSync) {
        using var bodyStream = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(bodyStream, ctx.RequestAborted);
        var bodyBytes = bodyStream.ToArray();
        var bodyText = Encoding.UTF8.GetString(bodyBytes);

        var signature = ctx.Request.Headers["X-Hub-Signature-256"].ToString();
        if (!SponsorWebhook.VerifySignature(config.SponsorConfig!.GitHubWebhookSecret, bodyBytes, signature))
            return Results.Unauthorized();

        SponsorshipWebhookEvent? evt;
        try {
            evt = SponsorWebhook.ParsePayload(bodyText);
        } catch (Exception exc) when (exc is JsonException or InvalidOperationException) {
            return Results.Ok();
        }
        if (evt is null) return Results.Ok();

        var isSponsor = SponsorWebhook.ResolveIsSponsor(evt.Action);
        if (isSponsor is null) return Results.Ok();

        await sponsorSync.ApplyWebhookEventAsync(evt.SponsorSubject, isSponsor.Value, ctx.RequestAborted);
        return Results.Ok();
    }
}
