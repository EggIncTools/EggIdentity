using System.Net;
using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.DbClone;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.SubProd;

public static class SubProdSignIn {
    public const string Gate = "signin";
    public const string DefaultPath = "/_subprod/sign-in";

    public static bool Enabled(Func<string, string?> environment) {
        ArgumentNullException.ThrowIfNull(environment);
        if (!SubProdFence.IsOpen(new FenceGate(Gate, []), environment)) return false;
        return SubProdFence.Applies(environment) ? true : throw new InvalidOperationException(
            $"{SubProdFence.AllowKey(Gate)} is set but {EnvironmentSettings.EnvKey} is not \"{EnvironmentSettings.SubProd}\". "
            + "Sub-prod sign-in issues sessions with no login at all and is refused outside sub-prod. Unset the key.");
    }

    public static IEndpointConventionBuilder? MapEggIdentitySubProdSignIn(
        this IEndpointRouteBuilder endpoints,
        SessionCookieOptions cookie,
        string path = DefaultPath,
        Func<string, string?>? environment = null) {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(cookie);
        if (!Enabled(environment ?? Environment.GetEnvironmentVariable)) return null;
        return endpoints.MapGet(path, (HttpContext ctx) => SignIn(ctx, cookie)).AllowAnonymous();
    }

    private static IResult SignIn(HttpContext ctx, SessionCookieOptions cookie) {
        if (!IsLoopback(ctx)) return Results.NotFound();
        var query = ctx.Request.Query;
        string? Value(string key) {
            return query.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString().Trim() : null;
        }

        var userId = Value("user");
        if (userId is null) return Results.BadRequest("user is required");

        var user = new SessionUser(
            userId, null, Value("role") ?? UserRoles.ToName(UserRole.Viewer),
            Value("name"), Value("avatar"), Value("discordId"));
        var now = (ctx.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System).GetUtcNow();
        ctx.Response.Cookies.Append(cookie.CookieName, SessionToken.Issue(cookie, user, now), new CookieOptions {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = now + cookie.Ttl,
        });
        return Results.Redirect(LocalPath(Value("returnUrl")));
    }

    internal static string LocalPath(string? returnUrl) =>
        returnUrl is "/" or ['/', not ('/' or '\\'), ..] ? returnUrl : "/";

    internal static bool IsLoopback(HttpContext ctx) =>
        !ctx.Request.Headers.ContainsKey("X-Forwarded-For")
        && !ctx.Request.Headers.ContainsKey("Forwarded")
        && ctx.Connection.RemoteIpAddress is { } remote
        && IPAddress.IsLoopback(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote)
        && IsLoopbackHost(ctx.Request.Host.Host);

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip));
}
