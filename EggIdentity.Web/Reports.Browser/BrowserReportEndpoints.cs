using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Reports.Browser;

public static class BrowserReportEndpointRouteBuilderExtensions {
    public static RouteGroupBuilder MapEggIdentityBrowserReports(this IEndpointRouteBuilder endpoints, string? prefix = null) {
        ArgumentNullException.ThrowIfNull(endpoints);
        var options = endpoints.ServiceProvider.GetRequiredService<BrowserReportOptions>();
        var catalog = endpoints.ServiceProvider.GetRequiredService<SourceDescriptorCatalog>();
        var cache = endpoints.ServiceProvider.GetRequiredService<SnapshotCache>();

        var group = endpoints.MapGroup(prefix ?? options.RoutePrefix);

        group.MapGet("descriptor/{source}", (string source) =>
            catalog.Descriptors.TryGetValue(source, out var descriptor)
                ? Results.Text(ReportWire.WriteDescriptor(descriptor), "application/json")
                : Results.NotFound());

        group.MapGet("schema/{source}", (string source) => {
            if (!catalog.Schemas.TryGetValue(source, out var schema)) return Results.NotFound();
            if (schema is null) return Results.NoContent();
            var json = JsonSerializer.Serialize(schema, ReportCodec.Options.GetTypeInfo<SnapshotSchema>());
            return Results.Text(json, "application/json");
        });

        group.MapGet("caps", (ReportEngineRegistry registry) => {
            var caps = new Dictionary<string, ReportTierCaps> {
                [nameof(ReportTier.Live)] = registry.Caps(ReportTier.Live),
                [nameof(ReportTier.Snapshot)] = registry.Caps(ReportTier.Snapshot),
            };
            var json = JsonSerializer.Serialize(caps, ReportCodec.Options.GetTypeInfo<Dictionary<string, ReportTierCaps>>());
            return Results.Text(json, "application/json");
        });

        group.MapGet("snapshot/{source}", async (string source, string? p, HttpContext ctx, CancellationToken ct) => {
            var scope = options.SharedSources.Contains(source) ? BrowserReportRegistry.SharedScope : options.ScopeKey(ctx.User);
            if (string.IsNullOrEmpty(scope)) return Results.Unauthorized();
            string version;
            try {
                version = await cache.VersionAsync(p, source, ctx.RequestServices, ct);
            } catch (KeyNotFoundException) {
                return Results.NotFound();
            }

            var etag = SnapshotCache.ETagFor(version);
            if (ctx.Request.Headers.IfNoneMatch.Any(v => v == etag)) return Results.StatusCode(StatusCodes.Status304NotModified);

            var snapshot = await cache.GetAsync(scope, p, source, version, ctx.RequestServices, ct);
            ctx.Response.Headers.ETag = snapshot.ETag;
            ctx.Response.Headers.CacheControl = "private, no-cache";
            return Results.Bytes(snapshot.Bytes, "application/octet-stream");
        });

        return group;
    }
}
