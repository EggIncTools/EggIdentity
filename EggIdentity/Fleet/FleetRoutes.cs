using System.Security.Cryptography;
using System.Text;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Fleet;

public static class FleetRoutes {
    public const string HookPath = "/hooks/image-pushed";
    private const string PlainText = "text/plain";
    private const string PinsUnconfigured = "pins not configured on the identity host";
    private const string BuildsUnconfigured = "builds not configured on the identity host";
    private const string SettingsUnconfigured = "settings not configured on the identity host";
    private const string AdminSessionRequired = "pins need an admin session";
    private const string PinUpdatedBy = "fleet pins";
    private const int DefaultBuildLimit = 50;
    private const int MaxBuildLimit = 500;

    private static readonly Func<string?, CancellationToken, Task<bool>> NoAdminSession = (_, _) => Task.FromResult(false);

    public static RouteGroupBuilder MapFleetAdminApi(
        this RouteGroupBuilder group, FleetRuntime runtime, Func<string?, CancellationToken, Task<bool>>? isAdminSession = null) {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(runtime);
        var service = runtime.Service;
        var fleet = group.MapGroup("/fleet").WithMetadata(new AdminCapability(AdminCapabilities.Fleet));
        MapPins(fleet, runtime, isAdminSession ?? NoAdminSession);

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

    private static void MapPins(RouteGroupBuilder fleet, FleetRuntime runtime, Func<string?, CancellationToken, Task<bool>> isAdminSession) {
        fleet.MapGet("/pins", (HttpContext ctx, CancellationToken ct) => GetPinsAsync(runtime, isAdminSession, ctx, ct));

        fleet.MapGet("/upstreams/{name}/builds", (string name, int? limit, HttpContext ctx, CancellationToken ct) =>
            GetBuildsAsync(runtime, isAdminSession, name, limit, ctx, ct));

        fleet.MapPut("/pins/{app}/{upstream}", (string app, string upstream, PinWriteRequest? body, HttpContext ctx, CancellationToken ct) =>
            PutPinRouteAsync(runtime, isAdminSession, app, upstream, body, ctx, ct));

        fleet.MapPost("/pins/take-latest", (TakeLatestRequest? body, HttpContext ctx, CancellationToken ct) =>
            TakeLatestRouteAsync(runtime, isAdminSession, body, ctx, ct));
    }

    private static async Task<IResult> PutPinRouteAsync(
        FleetRuntime runtime, Func<string?, CancellationToken, Task<bool>> isAdminSession,
        string app, string upstream, PinWriteRequest? body, HttpContext ctx, CancellationToken ct) {
        if (runtime.Pins is not { } pins) return Conflict(PinsUnconfigured);
        if (!await IsAdminAsync(ctx, isAdminSession, ct)) return Forbidden();
        if (body is null) return BadRequest("body is required");
        return SettingsAdmin(ctx) is { } admin ? await PutPinAsync(admin, pins, app, upstream, body, ct) : Conflict(SettingsUnconfigured);
    }

    private static async Task<IResult> TakeLatestRouteAsync(
        FleetRuntime runtime, Func<string?, CancellationToken, Task<bool>> isAdminSession, TakeLatestRequest? body, HttpContext ctx, CancellationToken ct) {
        if (runtime.Pins is not { } pins) return Conflict(PinsUnconfigured);
        if (!await IsAdminAsync(ctx, isAdminSession, ct)) return Forbidden();
        if (runtime.Builds is not { } builds) return Conflict(BuildsUnconfigured);
        var app = body?.App is { } named && !string.IsNullOrWhiteSpace(named) ? named.Trim() : null;
        return SettingsAdmin(ctx) is { } admin ? await TakeLatestAsync(admin, pins, builds, app, ct) : Conflict(SettingsUnconfigured);
    }

    private static async Task<IResult> GetPinsAsync(
        FleetRuntime runtime, Func<string?, CancellationToken, Task<bool>> isAdminSession, HttpContext ctx, CancellationToken ct) {
        if (runtime.Pins is not { } pins) return Conflict(PinsUnconfigured);
        if (!await IsAdminAsync(ctx, isAdminSession, ct)) return Forbidden();
        return Results.Ok(await pins.ViewsAsync(ct));
    }

    private static async Task<IResult> GetBuildsAsync(
        FleetRuntime runtime, Func<string?, CancellationToken, Task<bool>> isAdminSession, string name, int? limit, HttpContext ctx, CancellationToken ct) {
        if (runtime.Builds is not { } builds) return Conflict(BuildsUnconfigured);
        if (!await IsAdminAsync(ctx, isAdminSession, ct)) return Forbidden();
        var list = await builds.ListAsync(name, Math.Clamp(limit ?? DefaultBuildLimit, 1, MaxBuildLimit), ct);
        return Results.Ok(list.Select(b => new UpstreamBuildView(b.Upstream, b.Height, b.Sha, b.PublishedAt)).ToList());
    }

    private static async Task<IResult> PutPinAsync(
        ISettingsAdmin admin, PinReconciler pins, string app, string upstream, PinWriteRequest body, CancellationToken ct) {
        var desired = string.IsNullOrWhiteSpace(body.DesiredVersion) ? null : body.DesiredVersion.Trim();
        if (desired is not null && !IsBuildVersion(desired)) return InvalidVersion(desired);

        var id = SuiteUpstreams.PinId(app, upstream);
        var rows = await admin.GetRowsAsync(SuiteUpstreams.PinsKey, ct);
        if (rows.FirstOrDefault(r => SameId(r.Id, id)) is not { } row) return Results.NotFound();

        var values = PinValues(row, id, desired, body.FollowLatest);
        var problems = SuiteUpstreams.Problems(
            CollectionBinder.Bind<AppPin>(values),
            [.. (await admin.GetRowsAsync(SuiteApps.Key, ct)).Select(r => CollectionBinder.Bind<SuiteApp>(r.Values))],
            [.. (await admin.GetRowsAsync(SuiteUpstreams.UpstreamsKey, ct)).Select(r => CollectionBinder.Bind<SuiteUpstream>(r.Values))]);
        if (problems.Count > 0) return BadRequest(string.Join(" ", problems));

        var saved = await admin.SaveRowAsync(SuiteUpstreams.PinsKey, row.Id, values, PinUpdatedBy, ct);
        return saved.Ok ? Results.Ok(await ReconciledAsync(pins, app, ct)) : BadRequest(saved.Error ?? "pin save failed");
    }

    private static async Task<IResult> TakeLatestAsync(
        ISettingsAdmin admin, PinReconciler pins, IUpstreamBuilds builds, string? app, CancellationToken ct) {
        var rows = await admin.GetRowsAsync(SuiteUpstreams.PinsKey, ct);
        var latest = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows) {
            var pin = CollectionBinder.Bind<AppPin>(row.Values);
            if (app is not null && !SameId(pin.App, app)) continue;
            if (!latest.TryGetValue(pin.Upstream, out var version))
                latest[pin.Upstream] = version = (await builds.LatestAsync(pin.Upstream, ct))?.Version;
            if (version is null || string.Equals(pin.DesiredVersion?.Trim(), version, StringComparison.Ordinal)) continue;
            if (!IsBuildVersion(version)) return InvalidVersion(version);
            var saved = await admin.SaveRowAsync(SuiteUpstreams.PinsKey, row.Id, PinValues(row, pin.Id, version, pin.FollowLatest), PinUpdatedBy, ct);
            if (!saved.Ok) return BadRequest(saved.Error ?? "pin save failed");
        }
        return Results.Ok(await ReconciledAsync(pins, app, ct));
    }

    private static async Task<IReadOnlyList<PinView>> ReconciledAsync(PinReconciler pins, string? app, CancellationToken ct) {
        var reconciled = await pins.ReconcileAsync(app, ct);
        return app is null ? reconciled : [.. reconciled.Where(v => SameId(v.App, app))];
    }

    private static Dictionary<string, string?> PinValues(CollectionRow row, string id, string? desired, bool followLatest) =>
        new(row.Values, StringComparer.Ordinal) {
            ["id"] = id,
            ["desired_version"] = desired,
            ["follow_latest"] = followLatest ? "true" : "false",
        };

    private static ISettingsAdmin? SettingsAdmin(HttpContext ctx) =>
        ctx.RequestServices.GetService<ISettingsAdmin>()
        ?? (ctx.RequestServices.GetService<SettingsAdminService>() is { } service ? new LocalSettingsAdmin("", service) : null);

    private static Task<bool> IsAdminAsync(HttpContext ctx, Func<string?, CancellationToken, Task<bool>> isAdminSession, CancellationToken ct) =>
        ctx.Request.Headers.TryGetValue(IdentityWire.SessionHeader, out var token) && !string.IsNullOrWhiteSpace(token)
            ? isAdminSession(token.ToString(), ct)
            : Task.FromResult(false);

    private static bool IsBuildVersion(string value) => BuildVersion.Parse(value) is { Sha: null };

    private static bool SameId(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static IResult InvalidVersion(string version) => BadRequest($"invalid version \"{version}\"");

    private static IResult BadRequest(string text) => Results.Text(text, PlainText, null, StatusCodes.Status400BadRequest);

    private static IResult Conflict(string text) => Results.Text(text, PlainText, null, StatusCodes.Status409Conflict);

    private static IResult Forbidden() => Results.Text(AdminSessionRequired, PlainText, null, StatusCodes.Status403Forbidden);

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
            await RecordBuildAsync(runtime, service, app.Name, payload, ctx.RequestAborted);
            NotePins(runtime, service, app.Name, payload, ctx.RequestServices);
            return Results.Json(await service.StatusAsync(app.Name, ctx.RequestAborted), statusCode: StatusCodes.Status202Accepted);
        });
    }

    private static async Task RecordBuildAsync(FleetRuntime runtime, DeployService service, string appName, DeployHookPayload payload, CancellationToken ct) {
        if (runtime.Builds is not { } builds) return;
        if (payload is not { Height: { } h, Packages.Count: > 0 }) return;
        if (string.IsNullOrWhiteSpace(payload.Revision)) return;
        try {
            await builds.RecordAsync(appName, h, payload.Revision, ct);
        } catch (Exception e) when (!ct.IsCancellationRequested) {
            service.Logger?.LogWarning(e, "upstream build record failed for {App}", appName);
        }
    }

    private static void NotePins(FleetRuntime runtime, DeployService service, string appName, DeployHookPayload payload, IServiceProvider services) {
        if (runtime.Pins is not { } pins) return;
        var revision = payload.Revision;
        var reconcile = payload.Packages is { Count: > 0 };
        if (string.IsNullOrWhiteSpace(revision) && !reconcile) return;
        RunInBackground(services, async stopping => {
            if (!string.IsNullOrWhiteSpace(revision)) {
                try {
                    await pins.NoteHookAsync(appName, revision, stopping);
                } catch (Exception e) when (!stopping.IsCancellationRequested) {
                    service.Logger?.LogWarning(e, "pin hook note failed for {App}", appName);
                }
            }
            if (reconcile) await pins.ReconcileAsync(null, stopping);
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
                services.GetService<DeployService>()?.Logger?.LogError(e, "background work failed");
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
