using System.Diagnostics.CodeAnalysis;
using EggIdentity.Agent.Models.Stack;
using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace EggIdentity.Agent;

internal static class StackRoutes {
    public static void MapStackRoutes(this WebApplication app, AgentRuntime runtime) {
        app.MapGet("/stacks", async (HttpContext ctx) =>
            ctx.User.IsAtLeast(UserRole.Admin) ? await ListAsync(runtime, ctx.RequestAborted) : Results.Forbid());

        app.MapGet("/stacks/{stackName}/env", (string stackName, HttpContext ctx, IHttpClientFactory factory) =>
            ctx.User.IsAtLeast(UserRole.Admin) ? EnvAsync(runtime, factory, stackName, ctx.RequestAborted) : Task.FromResult(Results.Forbid()));

        app.MapPost("/stacks/{stackName}/redeploy", (string stackName, HttpContext ctx, IHostApplicationLifetime lifetime) =>
            ctx.User.IsAtLeast(UserRole.Admin) ? RedeployAsync(runtime, stackName, ctx.RequestAborted, lifetime.ApplicationStopping) : Task.FromResult(Results.Forbid()));

        app.MapPatch("/env/{appName}", (string appName, HttpContext ctx, IHttpClientFactory factory, Dictionary<string, string?> changes) =>
            ctx.User.IsAtLeast(UserRole.Admin) ? PatchEnvAsync(runtime, factory, appName, changes, ctx.RequestAborted) : Task.FromResult(Results.Forbid()));
    }

    private static async Task<IResult> ListAsync(AgentRuntime runtime, CancellationToken ct) {
        var infos = new List<StackInfo>();
        foreach (var stack in runtime.Service.Stacks) infos.Add(await DescribeAsync(runtime, stack, ct));
        return Results.Json(infos);
    }

    private static async Task<IResult> EnvAsync(AgentRuntime runtime, IHttpClientFactory factory, string stackName, CancellationToken ct) {
        if (!TryResolve(runtime, stackName, out var stack, out var portainer, out var refused)) return refused;
        try {
            var remote = await portainer.CreateClient(factory.CreateClient(), stack).GetStackAsync(ct);
            return Results.Json(remote.Env.OrderBy(e => e.Name, StringComparer.Ordinal).Select(MaskedEnvEntry.From));
        } catch (Exception e) when (e is not OperationCanceledException) {
            return Results.Problem(e.Message, statusCode: 502);
        }
    }

    private static async Task<IResult> RedeployAsync(AgentRuntime runtime, string stackName, CancellationToken ct, CancellationToken stopping) {
        if (!TryResolve(runtime, stackName, out var stack, out _, out var refused)) return refused;
        try {
            var readiness = await runtime.Stacks.ResolveAsync(stack, ct);
            if (!readiness.Ready) return Results.Problem(readiness.Info.Refusal, statusCode: 409);
            await runtime.Stacks.RedeployAsync(stack, readiness, stopping);
            return Results.Json(readiness.Info, statusCode: StatusCodes.Status202Accepted);
        } catch (StackBusyException e) {
            return Results.Problem(e.Message, statusCode: 409);
        } catch (Exception e) when (e is not OperationCanceledException) {
            return Results.Problem(e.Message, statusCode: 502);
        }
    }

    private static async Task<IResult> PatchEnvAsync(
        AgentRuntime runtime, IHttpClientFactory factory, string appName, Dictionary<string, string?> changes, CancellationToken ct) {
        if (changes is null || changes.Count == 0) return Results.BadRequest("no changes supplied");
        if (!runtime.Service.TryGetApp(appName, out _)) return Results.NotFound();
        if (runtime.Service.StackFor(appName) is not { } row)
            return Results.Problem($"deploy.apps row {appName} has no enabled deploy.stacks row", statusCode: 409);
        if (!TryResolve(runtime, row.Name, out var stack, out var portainer, out var refused)) return refused;
        try {
            await portainer.CreateClient(factory.CreateClient(), stack).UpdateEnvAsync(changes, ct);
            return Results.Json(new { updated = changes.Count, stack = stack.Name });
        } catch (StackBusyException e) {
            return Results.Problem(e.Message, statusCode: 409);
        } catch (Exception e) when (e is not OperationCanceledException) {
            return Results.Problem(e.Message, statusCode: 502);
        }
    }

    private static async Task<StackInfo> DescribeAsync(AgentRuntime runtime, DeployStack stack, CancellationToken ct) {
        try {
            return (await runtime.Stacks.ResolveAsync(stack, ct)).Info;
        } catch (Exception e) when (e is not OperationCanceledException) {
            return new StackInfo(stack.Name, stack.StackId, stack.EndpointId, null, false, null, null, false, false, e.Message);
        }
    }

    private static bool TryResolve(
        AgentRuntime runtime, string stackName,
        [NotNullWhen(true)] out DeployStack? stack,
        [NotNullWhen(true)] out PortainerConfig? portainer,
        [NotNullWhen(false)] out IResult? refused) {
        stack = runtime.Service.Stacks.FirstOrDefault(s => string.Equals(s.Name, stackName, StringComparison.OrdinalIgnoreCase));
        portainer = runtime.Portainer;
        if (stack is null) {
            refused = Results.NotFound();
            return false;
        }
        if (portainer is null) {
            refused = Results.Problem("portainer is not configured", statusCode: 503);
            return false;
        }
        if (!stack.HasPortainerIds) {
            refused = Results.Problem($"deploy.stacks row {stack.Name} has no stack_id and endpoint_id", statusCode: 409);
            return false;
        }
        refused = null;
        return true;
    }
}
