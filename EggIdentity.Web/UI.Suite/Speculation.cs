using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace EggIdentity.UI.Suite;

public static class Speculation {
    public const string LoadingModeHeader = "Supports-Loading-Mode";
    public const string CredentialedPrerender = "credentialed-prerender";

    public static bool IsSpeculative(HttpContext? ctx) {
        if (ctx is null) return false;
        var headers = ctx.Request.Headers;
        return headers["Sec-Purpose"].ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase)
            || headers["Purpose"].ToString().Trim().Equals("prefetch", StringComparison.OrdinalIgnoreCase);
    }

    public static IApplicationBuilder UseSuitePrerender(this IApplicationBuilder app) {
        ArgumentNullException.ThrowIfNull(app);
        return app.Use(async (ctx, next) => {
            ctx.Response.OnStarting(() => {
                if (ctx.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
                    ctx.Response.Headers[LoadingModeHeader] = CredentialedPrerender;
                return Task.CompletedTask;
            });
            await next(ctx);
        });
    }
}
