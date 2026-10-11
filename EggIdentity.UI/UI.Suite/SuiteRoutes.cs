using EggIdentity.Auth;
using EggIdentity.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.UI.Suite;

public static class SuiteRoutes {
    public const string AvatarPath = "/_suite/avatar";
    private const string DefaultSessionCookie = "eggidentity_session";

    public static IEndpointConventionBuilder MapEggIdentitySuite(this IEndpointRouteBuilder routes) {
        ArgumentNullException.ThrowIfNull(routes);
        return routes.MapPost(AvatarPath, UploadAvatarAsync).DisableAntiforgery();
    }

    internal static async Task<IResult> UploadAvatarAsync(HttpContext ctx, UploadStore uploads) {
        if (ctx.Request.Headers["Sec-Fetch-Site"].ToString() is { Length: > 0 } site && site != "same-origin")
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        var name = ctx.RequestServices.GetService<SessionCookieOptions>()?.CookieName ?? DefaultSessionCookie;
        var api = ctx.RequestServices.GetService<IdentityApiClient>();
        if (api is null || !ctx.Request.Cookies.TryGetValue(name, out var token) || !ctx.Request.HasFormContentType)
            return await UploadRoutes.CompleteAsync(ctx, uploads, StatusCodes.Status401Unauthorized);

        var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
        if (form.Files["file"] is not { } file) return await UploadRoutes.CompleteAsync(ctx, uploads, StatusCodes.Status400BadRequest);
        await using var stream = file.OpenReadStream();
        var ok = await api.UploadAvatarAsync(token, stream, file.FileName, file.ContentType, ctx.RequestAborted);
        return await UploadRoutes.CompleteAsync(ctx, uploads, ok ? StatusCodes.Status204NoContent : StatusCodes.Status400BadRequest);
    }
}
