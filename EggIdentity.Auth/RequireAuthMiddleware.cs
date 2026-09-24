using Microsoft.AspNetCore.Http;

namespace EggIdentity.Auth;

public interface ISessionStore {
    Task<(bool Found, string DiscordId, long ExpiresAt)> LookupAsync(string token, CancellationToken ct);
    Task TouchAsync(string token, long newExpiresAt, CancellationToken ct);
}

public sealed class RequireAuth(RequestDelegate next, ISessionStore store, TimeProvider? time = null) {
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public const string UserIdHeader = "X-User-Id";

    public static string ExtractToken(string header) =>
        header.StartsWith("Bearer ", StringComparison.Ordinal) ? header["Bearer ".Length..] : header;

    public async Task Invoke(HttpContext ctx) {
        var token = ExtractToken(ctx.Request.Headers.Authorization.ToString());
        if (string.IsNullOrEmpty(token)) {
            await Unauthorized(ctx);
            return;
        }
        var (found, discordId, expiresAt) = await store.LookupAsync(token, ctx.RequestAborted);
        var now = _time.GetUtcNow();
        if (!found || now.ToUnixTimeSeconds() > expiresAt) {
            await Unauthorized(ctx);
            return;
        }
        var slid = now.AddDays(30).ToUnixTimeSeconds();
        await store.TouchAsync(token, slid, ctx.RequestAborted);
        ctx.Request.Headers[UserIdHeader] = discordId;
        await next(ctx);
    }

    private static async Task Unauthorized(HttpContext ctx) {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsync("unauthorized");
    }
}
