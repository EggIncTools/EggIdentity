using System.Security.Claims;
using EggIdentity.Auth;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.Host;

public static class ProfileAuth {
    public static async Task<Guid?> TryGetUserIdAsync(
        HttpContext ctx, SessionCookieOptions cookie, Func<string, CancellationToken, Task<bool>> isRevokedAsync, CancellationToken ct) {
        var token = ctx.Request.Headers.TryGetValue("X-EggIdentity-Session", out var header) ? header.ToString()
            : ctx.Request.Cookies.TryGetValue(cookie.CookieName, out var cookieValue) ? cookieValue
            : null;
        if (string.IsNullOrEmpty(token)) return null;

        var principal = SessionToken.Validate(cookie, token, DateTimeOffset.UtcNow);
        if (principal is null) return null;

        var userId = principal.EggIdentityUserId();
        if (userId is null) return null;

        var sid = principal.FindFirstValue(SessionClaims.SessionId);
        if (!string.IsNullOrEmpty(sid) && await isRevokedAsync(sid, ct)) return null;

        return userId;
    }
}
