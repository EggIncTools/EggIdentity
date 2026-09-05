using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Agent;

internal static class EnvRoutes {
    public static void MapEnvRoutes(this WebApplication app, AgentRuntime runtime) {
        app.MapGet("/env/{appName}", async (string appName, HttpContext ctx, IHttpClientFactory factory) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (!runtime.Service.TryGetApp(appName, out var cfg)) return Results.NotFound();

            var ct = ctx.RequestAborted;
            ContainerInfo? container;
            try {
                container = await runtime.Engine.InspectContainerAsync(cfg.ContainerName, ct);
            } catch (Exception e) when (e is not OperationCanceledException) {
                return Results.Problem($"docker inspect failed: {e.Message}", statusCode: 502);
            }
            if (container is null) return Results.Problem($"container {cfg.ContainerName} not found", statusCode: 404);

            var imageEnv = await ImageEnvAsync(runtime.Engine, container, ct);
            ComposeServiceInfo? compose = null;
            IReadOnlyList<StackEnvEntry> stackVariables = [];
            if (runtime.Portainer is not null)
                (compose, stackVariables) = await PortainerViewAsync(runtime.Portainer.CreateClient(factory.CreateClient()), cfg.ContainerName, ct);

            return Results.Json(EnvProvenance.Build(compose, container.Env, imageEnv, stackVariables));
        });
    }

    private static async Task<IReadOnlyList<string>> ImageEnvAsync(IDockerEngine engine, ContainerInfo container, CancellationToken ct) {
        var reference = container.ImageId.Length > 0 ? container.ImageId : container.Image;
        if (reference.Length == 0) return [];
        try {
            var image = await engine.InspectImageAsync(reference, ct);
            return image?.Env ?? [];
        } catch (Exception e) when (e is not OperationCanceledException) {
            Console.Error.WriteLine($"eggidentity-agent: image inspect for {container.Name} failed: {e.Message}");
            return [];
        }
    }

    private static async Task<(ComposeServiceInfo? Compose, IReadOnlyList<StackEnvEntry> Variables)> PortainerViewAsync(
        PortainerClient client, string serviceName, CancellationToken ct) {
        try {
            var file = await client.GetStackFileAsync(ct);
            var env = await client.GetEnvAsync(ct);
            var compose = file.Ok ? ComposeEnv.Parse(file.Compose, serviceName) : null;
            return (compose, env.Ok ? env.Entries : []);
        } catch (Exception e) when (e is not OperationCanceledException) {
            Console.Error.WriteLine($"eggidentity-agent: portainer lookup for {serviceName} failed: {e.Message}");
            return (null, []);
        }
    }
}
