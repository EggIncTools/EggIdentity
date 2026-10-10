using EggIdentity.Contract;
using EggIdentity.Settings.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Settings.Api;

public sealed record AdminApiOptions(string AppName, string Secret) {
    public string Prefix { get; init; } = "/admin/api";
    public string? Version { get; init; }
    public string? Revision { get; init; }
}

public static class AdminApiRoutes {
    public static RouteGroupBuilder MapAdminApi(this IEndpointRouteBuilder routes, AdminApiOptions options) {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Secret);

        var group = routes.MapGroup(options.Prefix).AddEndpointFilter(new BearerFilter(options.Secret));
        var prefix = options.Prefix.TrimEnd('/');

        group.MapGet("/manifest", ([FromServices] IServiceProvider services, [FromServices] EndpointDataSource endpoints) =>
            Results.Ok(new AdminManifest {
                App = options.AppName,
                Version = options.Version,
                Revision = options.Revision,
                Capabilities = Capabilities(endpoints, prefix, services.GetService<IRestartTrigger>() is not null),
            }));

        group.MapGet("/settings", async ([FromServices] IServiceProvider services, CancellationToken ct) => {
            if (Admin(services) is not { } admin) return Unconfigured();
            var rows = await admin.GetRowsAsync(ct);
            return Results.Ok(new AdminSettingsResponse {
                App = options.AppName,
                Settings = [.. rows.Select(AdminWireMapping.ToWire)],
                PendingRestartKeys = admin.PendingRestartKeys,
            });
        }).WithMetadata(new AdminCapability(AdminCapabilities.Settings));

        group.MapPut("/settings/{key}", async (
            string key, AdminSaveRequest body, [FromServices] IServiceProvider services, CancellationToken ct) => {
                if (Admin(services) is not { } admin) return Unconfigured();
                var result = await admin.SaveAsync(key, body.Value, body.UpdatedBy, ct);
                return Results.Ok(AdminWireMapping.ToWire(result));
            });

        group.MapGet("/collections", ([FromServices] IServiceProvider services) =>
            Admin(services) is not { } admin
                ? Unconfigured()
                : Results.Ok(admin.Collections.Select(AdminWireMapping.ToWire)))
            .WithMetadata(new AdminCapability(AdminCapabilities.Collections));

        group.MapGet("/collections/{key}", async (string key, [FromServices] IServiceProvider services, CancellationToken ct) => {
            if (Admin(services) is not { } admin) return Unconfigured();
            var descriptor = admin.Collections.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
            if (descriptor is null) return Results.NotFound();
            var rows = await admin.GetRowsAsync(key, ct);
            return Results.Ok(new AdminCollectionResponse {
                Descriptor = AdminWireMapping.ToWire(descriptor),
                Rows = [.. rows.Select(AdminWireMapping.ToWire)],
            });
        });

        group.MapPost("/collections/{key}", async (
            string key, AdminRowRequest body, [FromServices] IServiceProvider services, CancellationToken ct) => {
                if (Admin(services) is not { } admin) return Unconfigured();
                var result = await admin.CreateRowAsync(key, body.Id, body.Values, body.UpdatedBy, ct);
                return Results.Ok(AdminWireMapping.ToWire(result));
            });

        group.MapPut("/collections/{key}/{id}", async (
            string key, string id, AdminRowRequest body, [FromServices] IServiceProvider services, CancellationToken ct) => {
                if (Admin(services) is not { } admin) return Unconfigured();
                var result = await admin.SaveRowAsync(key, id, body.Values, body.UpdatedBy, ct);
                return Results.Ok(AdminWireMapping.ToWire(result));
            });

        group.MapDelete("/collections/{key}/{id}", async (
            string key, string id, [FromServices] IServiceProvider services, CancellationToken ct) => {
                if (Admin(services) is not { } admin) return Unconfigured();
                var result = await admin.DeleteRowAsync(key, id, ct);
                return Results.Ok(AdminWireMapping.ToWire(result));
            });

        group.MapGet("/drift", async (
            [FromServices] IServiceProvider services, CancellationToken ct) => {
                if (Admin(services) is not { } admin) return Unconfigured();
                var source = services.GetService<IEnvSource>()
                    ?? new ProcessEnvSource(services.GetRequiredService<SettingsRegistry>());
                var env = await source.GetAsync(ct);
                return Results.Ok(AdminWireMapping.ToWire(options.AppName, await admin.DriftAsync(env, ct)));
            }).WithMetadata(new AdminCapability(AdminCapabilities.Drift));

        group.MapPost("/restart", async ([FromServices] IServiceProvider services, CancellationToken ct) => {
            if (services.GetService<IRestartTrigger>() is not { } restart)
                return Results.BadRequest(new AdminSaveResponse { Ok = false, Error = "restart is not available for this app" });
            var failure = await restart.RestartAsync(ct);
            return Results.Ok(new AdminSaveResponse { Ok = failure is null, Error = failure });
        });

        return group;
    }

    internal static IReadOnlyList<string> Capabilities(EndpointDataSource endpoints, string prefix, bool canRestart) {
        var names = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => ("/" + (e.RoutePattern.RawText ?? "").TrimStart('/')).StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            .SelectMany(e => e.Metadata.GetOrderedMetadata<AdminCapability>())
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (canRestart) names.Add(AdminCapabilities.Restart);
        return [.. names.Order(StringComparer.Ordinal)];
    }

    private static SettingsAdminService? Admin(IServiceProvider services) =>
        services.GetService<SettingsAdminService>();

    private static IResult Unconfigured() =>
        Results.Json(
            new AdminSaveResponse { Ok = false, Error = "this app has no settings admin service registered" },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private sealed class BearerFilter(string secret) : IEndpointFilter {
        private readonly string _expected = $"Bearer {secret}";

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(next);

            var provided = context.HttpContext.Request.Headers.Authorization.ToString();
            return FixedTimeEquals(provided, _expected) ? await next(context) : Results.Unauthorized();
        }

        private static bool FixedTimeEquals(string a, string b) {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
