using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Fallback;

public static class FallbackMiddlewareExtensions {
    public static void AddEggIdentityFallback(this IServiceCollection services, FallbackBranding branding) {
        services.AddSingleton(branding);
        services.AddSingleton<MaintenanceState>();
    }

    public static void UseEggIdentityFallback(this WebApplication app) {
        var branding = app.Services.GetRequiredService<FallbackBranding>();
        var maintenance = app.Services.GetRequiredService<MaintenanceState>();

        app.UseExceptionHandler(errApp => errApp.Run(async ctx => {
            var feature = ctx.Features.Get<IExceptionHandlerFeature>();
            var exception = feature?.Error ?? new InvalidOperationException("unknown error");
            app.Logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", ctx.TraceIdentifier);
            var showTrace = ctx.User.IsAtLeast(UserRole.Admin);
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(FallbackPages.RenderError(branding, exception, showTrace, ctx.TraceIdentifier));
        }));

        app.Use(async (ctx, next) => {
            if (maintenance.IsOn && !ctx.User.IsAtLeast(UserRole.Admin)) {
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync(FallbackPages.RenderMaintenance(branding));
                return;
            }
            await next();
        });

        app.MapPost("/admin/maintenance/{state}", (string state, HttpContext ctx) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (state is not ("on" or "off")) return Results.BadRequest("state must be \"on\" or \"off\"");
            maintenance.Set(state == "on");
            return Results.NoContent();
        });

        app.Use(async (ctx, next) => {
            await next();
            if (ctx.Response.StatusCode == StatusCodes.Status404NotFound && !ctx.Response.HasStarted) {
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync(FallbackPages.RenderNotFound(branding));
            }
        });
    }
}
