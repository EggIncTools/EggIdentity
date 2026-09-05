using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Agent;

public static class StackRoutes {
    private const string NotConfigured = "portainer is not configured";

    public static void MapStackRoutes(this WebApplication app, PortainerConfig? portainer) {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/stack/env", async (HttpContext ctx, IHttpClientFactory factory) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (portainer is null) return Results.Problem(NotConfigured, statusCode: 503);

            var result = await portainer.CreateClient(factory.CreateClient()).GetEnvAsync(ctx.RequestAborted);
            if (!result.Ok) return Results.Problem(result.Error, statusCode: 502);

            return Results.Json(result.Entries
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .Select(e => new { name = e.Name, value = SecretMasking.Mask(e.Name, e.Value), masked = SecretMasking.LooksSecret(e.Name) }));
        });

        app.MapPatch("/stack/env", async (HttpContext ctx, IHttpClientFactory factory, Dictionary<string, string?> changes) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (changes is null || changes.Count == 0) return Results.BadRequest("no changes supplied");
            if (portainer is null) return Results.Problem(NotConfigured, statusCode: 503);

            var result = await portainer.CreateClient(factory.CreateClient()).PatchEnvAsync(changes, ctx.RequestAborted);
            if (!result.Ok) return Results.Problem(result.Error, statusCode: 502);

            return Results.Json(new { updated = changes.Count });
        });

        app.MapPost("/stack/reconcile", async (HttpContext ctx, IHttpClientFactory factory) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (portainer is null) return Results.Problem(NotConfigured, statusCode: 503);

            var result = await portainer.CreateClient(factory.CreateClient()).ReconcileAsync(ctx.RequestAborted);
            if (!result.Ok) return Results.Problem(result.Error, statusCode: 502);

            return Results.Json(new { reconciled = true, variables = result.Entries.Count });
        });
    }
}
