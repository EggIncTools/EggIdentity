using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.UI;

public static class ViewerCookieRoutes {
    public static IServiceCollection AddEggIdentityViewerCookies(this IServiceCollection services, Action<ViewerCookieOptions>? configure = null) {
        configure?.Invoke(ViewerCookieOptions.In(services));
        return services;
    }

    public static IEndpointConventionBuilder MapEggIdentityViewerCookies(this IEndpointRouteBuilder routes) {
        ArgumentNullException.ThrowIfNull(routes);
        var options = routes.ServiceProvider.GetRequiredService<ViewerCookieOptions>();
        return routes.MapGet(options.Path, (HttpContext ctx) => Write(ctx, options)).AllowAnonymous();
    }

    public static IResult Write(HttpContext ctx, ViewerCookieOptions options) {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(options);
        if (ctx.Request.Headers["Sec-Fetch-Site"].ToString() is { Length: > 0 } site && site != "same-origin")
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var name = ctx.Request.Query["name"].ToString();
        var value = ctx.Request.Query["value"].ToString();
        if (!options.Cookies.TryGetValue(name, out var rule) || value.Length > ViewerCookieOptions.MaxValueLength)
            return Results.BadRequest();

        ctx.Response.Headers.CacheControl = "no-store";
        if (ctx.Request.Query["remove"].ToString() == "1") {
            ctx.Response.Cookies.Delete(name, new CookieOptions { Path = "/", Domain = rule.Domain, Secure = true });
            return Results.NoContent();
        }

        ctx.Response.Cookies.Append(name, value, new CookieOptions {
            Path = "/",
            Domain = rule.Domain,
            MaxAge = rule.Lifetime,
            SameSite = SameSiteMode.Lax,
            Secure = true,
            IsEssential = true,
        });
        return Results.NoContent();
    }
}
