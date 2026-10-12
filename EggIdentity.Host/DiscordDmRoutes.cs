using EggIdentity.Auth;
using EggIdentity.Bot;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Settings.Store;
using Microsoft.AspNetCore.Mvc;

namespace EggIdentity.Host;

internal static class DiscordDmRoutes {
    public static void Map(WebApplication app, HostConfig config) {
        if (config is not { SessionOptions: { } sessionOptions }) return;
        var revocations = app.Services.GetRequiredService<RevocationStore>();
        app.MapGet("/profile/discord/dm-reachability",
            ([FromQuery(Name = "app")] string? appName, HttpContext ctx, SettingsCache cache, UserQueries users, IHttpClientFactory httpClientFactory) =>
                ReachabilityAsync(appName, ctx, sessionOptions, revocations, cache, users, httpClientFactory));
    }

    private static async Task<IResult> ReachabilityAsync(
        string? appName, HttpContext ctx, SessionCookieOptions sessionOptions, RevocationStore revocations,
        SettingsCache cache, UserQueries users, IHttpClientFactory httpClientFactory) {
        var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, revocations.IsRevokedAsync, ctx.RequestAborted);
        if (userId is null) return Results.Unauthorized();

        var row = (await cache.GetAsync(ctx.RequestAborted)).Collection<SuiteApp>(SuiteApps.Key)
            .FirstOrDefault(a => string.Equals(a.Name, appName, StringComparison.OrdinalIgnoreCase));
        if (DiscordRegistrationRoutes.ToResponse(row) is not { } registration) return Results.NotFound();

        var user = await users.GetAsync(userId.Value, ctx.RequestAborted);
        if (user is null) return Results.Unauthorized();

        var state = user.DiscordId is not { Length: > 0 } discordId
            ? DmReachability.NoDiscordLink
            : await CheckAsync(new DiscordDmClient(httpClientFactory, registration.Token), registration.GuildId, discordId, ctx.RequestAborted);
        return Results.Ok(new DmReachabilityResponse { State = state, GuildInviteUrl = registration.GuildInviteUrl });
    }

    private static async Task<DmReachability> CheckAsync(DiscordDmClient client, string guildId, string discordId, CancellationToken ct) {
        try {
            return await client.IsGuildMemberAsync(guildId, discordId, ct) switch {
                true => DmReachability.Reachable,
                false => DmReachability.NotInGuild,
                null => DmReachability.Unknown,
            };
        } catch (Exception exc) when (exc is not OperationCanceledException || !ct.IsCancellationRequested) {
            Console.Error.WriteLine($"dm reachability check failed: {exc.Message}");
            return DmReachability.Unknown;
        }
    }
}
