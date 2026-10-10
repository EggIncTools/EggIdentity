using System.Security.Cryptography;
using System.Text;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EggIdentity.Fleet;

public static class FleetRoutes {
    public const string HookPath = "/hooks/image-pushed";
    private const string PlainText = "text/plain";

    public static RouteGroupBuilder MapFleetAdminApi(this RouteGroupBuilder group, FleetRuntime runtime) {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(runtime);
        var service = runtime.Service;
        var fleet = group.MapGroup("/fleet").WithMetadata(new AdminCapability(AdminCapabilities.Fleet));

        fleet.MapGet("/status", async (CancellationToken ct) => Results.Ok(await service.StatusAllAsync(ct)));

        fleet.MapGet("/status/{app}", async (string app, CancellationToken ct) =>
            await service.StatusAsync(app, ct) is { } status ? Results.Ok(status) : Results.NotFound());

        fleet.MapPost("/check/{app}", async (string app, CancellationToken ct) =>
            await service.CheckAsync(app, ct) is { } status ? Results.Ok(status) : Results.NotFound());

        fleet.MapPost("/deploy/{app}", async (string app, IServiceProvider services, CancellationToken ct) => {
            if (await service.StatusAsync(app, ct) is not { } status) return Results.NotFound();
            RunInBackground(services, stopping => service.DeployAsync(app, "manual", stopping));
            return Results.Json(status, statusCode: StatusCodes.Status202Accepted);
        });

        fleet.MapPost("/restart/{app}", async (string app, CancellationToken ct) => {
            if (await service.FindAppAsync(app, ct) is null) return Results.NotFound();
            var failure = await service.RestartAsync(app, ct);
            return Results.Ok(new AdminSaveResponse { Ok = failure is null, Error = failure });
        });

        fleet.MapGet("/logs/{app}", async (string app, int? lines, CancellationToken ct) => {
            try {
                var (text, refusal) = await service.LogsAsync(app, lines ?? 200, ct);
                return text is not null ? Results.Text(text, PlainText) : Results.Text(refusal, PlainText, null, StatusCodes.Status409Conflict);
            } catch (Exception e) when (e is not OperationCanceledException) {
                return Results.Text($"docker logs failed: {e.Message}", PlainText, null, StatusCodes.Status502BadGateway);
            }
        });

        fleet.MapGet("/events", async ctx => {
            var after = ServerSentEvents.ResolveAfter(ctx.Request.Headers["Last-Event-ID"], ctx.Request.Query["after"]);
            if (after < service.Events.FirstId - 1) after = 0;
            await ServerSentEvents.StreamAsync(ctx.Response, service.Events, after, ServerSentEvents.KeepaliveInterval, ctx.RequestAborted);
        });

        fleet.MapGet("/stacks", (CancellationToken ct) => Lookup(async () => (await service.StacksAsync(ct), (string?)null)));

        fleet.MapPost("/stacks/{name}/redeploy", async (string name, CancellationToken ct) =>
            Results.Ok(await Guard(() => service.RedeployStackAsync(name, ct))));

        fleet.MapGet("/portainer/stacks", (CancellationToken ct) => Lookup(() => service.PortainerStacksAsync(ct)));

        fleet.MapGet("/portainer/stacks/{name}/services", (string name, CancellationToken ct) =>
            Lookup(() => service.StackServicesAsync(name, ct)));

        fleet.MapGet("/env/{app}", (string app, CancellationToken ct) => Lookup(() => service.EnvAsync(app, ct)));

        fleet.MapPatch("/env/{app}", async (string app, Dictionary<string, string?> changes, CancellationToken ct) =>
            Results.Ok(await Guard(() => service.PatchEnvAsync(app, changes, ct))));

        return group;
    }

    public static IEndpointConventionBuilder MapFleetHook(this IEndpointRouteBuilder routes, FleetRuntime runtime) {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(runtime);
        var service = runtime.Service;
        return routes.MapPost(HookPath, async (HttpContext ctx, DeployHookPayload? payload) => {
            if (!BearerMatches(ctx.Request, runtime.HookSecret))
                return Results.Text("unauthorized", PlainText, null, StatusCodes.Status401Unauthorized);
            if (payload is null || string.IsNullOrWhiteSpace(payload.App)) return Results.BadRequest("app is required");
            if (await service.FindAppAsync(payload.App, ctx.RequestAborted) is not { } app) return Results.NotFound();

            service.NoteRelease(app.Name, payload.Digest, payload.Revision, payload.Version);
            RunInBackground(ctx.RequestServices, async stopping => {
                var status = await service.CheckAsync(app.Name, stopping);
                if (app.AutoDeploy && status is { UpdateAvailable: true })
                    await service.DeployAsync(app.Name, "hook", stopping);
            });
            return Results.Json(await service.StatusAsync(app.Name, ctx.RequestAborted), statusCode: StatusCodes.Status202Accepted);
        });
    }

    private static async Task<IResult> Lookup<T>(Func<Task<(T? Value, string? Refusal)>> work) where T : class {
        try {
            var (value, refusal) = await work();
            return value is not null ? Results.Ok(value) : Results.Problem(refusal, statusCode: StatusCodes.Status409Conflict);
        } catch (Exception e) when (e is not OperationCanceledException) {
            return Results.Problem(e.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task<AdminSaveResponse> Guard(Func<Task<string?>> work) {
        try {
            var failure = await work();
            return new AdminSaveResponse { Ok = failure is null, Error = failure };
        } catch (StackBusyException e) {
            return new AdminSaveResponse { Ok = false, Error = e.Message };
        } catch (Exception e) when (e is not OperationCanceledException) {
            return new AdminSaveResponse { Ok = false, Error = e.Message };
        }
    }

    private static void RunInBackground(IServiceProvider services, Func<CancellationToken, Task> work) {
        var stopping = services.GetService<IHostApplicationLifetime>()?.ApplicationStopping ?? CancellationToken.None;
        _ = Task.Run(async () => {
            try {
                await work(stopping);
            } catch (Exception e) when (e is not OperationCanceledException) {
                Console.Error.WriteLine($"fleet: background work failed: {e}");
            }
        }, CancellationToken.None);
    }

    internal static bool BearerMatches(HttpRequest request, string? secret) {
        if (string.IsNullOrEmpty(secret)) return false;
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return false;
        var token = header["Bearer ".Length..].Trim();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret));
    }
}
