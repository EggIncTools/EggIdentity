using System.Security.Claims;
using EggIdentity.Auth;
using EggIdentity.Contract;

namespace EggIdentity.Host;

public static class ProfileAuth {
    public static async Task<Guid?> TryGetUserIdAsync(
        HttpContext ctx, SessionCookieOptions cookie, Func<string, CancellationToken, Task<bool>> isRevokedAsync, CancellationToken ct) =>
        (await TryGetPrincipalAsync(ctx, cookie, isRevokedAsync, ct))?.EggIdentityUserId();

    public static async Task<ClaimsPrincipal?> TryGetPrincipalAsync(
        HttpContext ctx, SessionCookieOptions cookie, Func<string, CancellationToken, Task<bool>> isRevokedAsync, CancellationToken ct) {
        var token = ReadToken(ctx, cookie);
        if (string.IsNullOrEmpty(token)) return null;

        var principal = SessionToken.Validate(cookie, token, DateTimeOffset.UtcNow);
        if (principal?.EggIdentityUserId() is null) return null;

        var sid = principal.FindFirstValue(SessionClaims.SessionId);
        return !string.IsNullOrEmpty(sid) && await isRevokedAsync(sid, ct) ? null : principal;
    }

    private static string? ReadToken(HttpContext ctx, SessionCookieOptions cookie) {
        if (ctx.Request.Headers.TryGetValue(IdentityWire.SessionHeader, out var header)) return header.ToString();
        return ctx.Request.Cookies.TryGetValue(cookie.CookieName, out var cookieValue) ? cookieValue : null;
    }
}
