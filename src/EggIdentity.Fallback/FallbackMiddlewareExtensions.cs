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
            var showTrace = IsAdmin(ctx, branding);
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            if (WantsJson(ctx)) {
                await ctx.Response.WriteAsJsonAsync(new { error = "internal_server_error", traceId = ctx.TraceIdentifier, trace = showTrace ? exception.ToString() : null });
            } else {
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync(FallbackPages.RenderError(branding, exception, showTrace, ctx.TraceIdentifier));
            }
        }));

        app.Use(async (ctx, next) => {
            if (maintenance.IsOn && !IsAdmin(ctx, branding)) {
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                if (WantsJson(ctx)) {
                    await ctx.Response.WriteAsJsonAsync(new { error = "maintenance" });
                } else {
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.WriteAsync(FallbackPages.RenderMaintenance(branding));
                }
                return;
            }
            await next();
        });

        app.MapPost("/admin/maintenance/{state}", (string state, HttpContext ctx) => {
            if (!IsAdmin(ctx, branding)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (state is not ("on" or "off")) return Results.BadRequest("state must be \"on\" or \"off\"");
            maintenance.Set(state == "on");
            return Results.NoContent();
        });

        app.MapGet("/admin/maintenance", (HttpContext ctx) => {
            if (!IsAdmin(ctx, branding)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Content(FallbackPages.RenderMaintenanceAdmin(branding, maintenance.IsOn), "text/html");
        });

        app.Use(async (ctx, next) => {
            await next();
            if (ctx.Response.StatusCode == StatusCodes.Status404NotFound && !ctx.Response.HasStarted) {
                if (WantsJson(ctx)) {
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsJsonAsync(new { error = "not_found" });
                } else {
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.WriteAsync(FallbackPages.RenderNotFound(branding));
                }
            }
        });
    }

    private static bool IsAdmin(HttpContext ctx, FallbackBranding branding) {
        if (branding.RoleClaimType is null) return ctx.User.IsAtLeast(UserRole.Admin);
        var role = UserRoles.Parse(ctx.User.FindFirst(branding.RoleClaimType)?.Value);
        return UserRoles.IsAtLeast(role, UserRole.Admin);
    }

    private static bool WantsJson(HttpContext ctx) {
        var accept = ctx.Request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
