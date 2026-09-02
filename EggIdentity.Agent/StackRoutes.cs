using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Agent;

public static class StackRoutes {
    public static void MapStackRoutes(this WebApplication app, AgentRegistry registry) {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(registry);

        app.MapGet("/stack/env", async (HttpContext ctx, IHttpClientFactory factory) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();

            var client = PortainerClient.FromEnvironment(factory.CreateClient());
            if (client is null) return Results.Problem("portainer is not configured", statusCode: 503);

            var result = await client.GetEnvAsync(ctx.RequestAborted);
            if (!result.Ok) return Results.Problem(result.Error, statusCode: 502);

            return Results.Json(result.Entries
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .Select(e => new { name = e.Name, value = SecretMasking.Mask(e.Name, e.Value), masked = SecretMasking.LooksSecret(e.Name) }));
        });

        app.MapPatch("/stack/env", async (HttpContext ctx, IHttpClientFactory factory, Dictionary<string, string?> changes) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (changes is null || changes.Count == 0) return Results.BadRequest("no changes supplied");

            var client = PortainerClient.FromEnvironment(factory.CreateClient());
            if (client is null) return Results.Problem("portainer is not configured", statusCode: 503);

            var result = await client.PatchEnvAsync(changes, ctx.RequestAborted);
            if (!result.Ok) return Results.Problem(result.Error, statusCode: 502);

            return Results.Json(new { updated = changes.Count });
        });

        app.MapPost("/restart/{appName}", async (string appName, HttpContext ctx) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (!registry.Apps.ContainsKey(appName)) return Results.NotFound();

            var failure = await DockerContainer.RestartAsync(appName, ctx.RequestAborted);
            return failure is null ? Results.Ok() : Results.Problem(failure, statusCode: 502);
        });
    }
}
