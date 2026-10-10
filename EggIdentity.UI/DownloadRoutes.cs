using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EggIdentity.UI;

public sealed class DownloadOptions {
    public string Path { get; init; } = "/_download";
}

public static class DownloadRoutes {
    public static IServiceCollection AddEggIdentityDownloads(this IServiceCollection services, DownloadOptions? options = null) {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(options ?? new DownloadOptions());
        services.AddSingleton<DownloadStore>();
        services.AddScoped<Downloads>();
        return services;
    }

    public static IEndpointConventionBuilder MapEggIdentityDownloads(this IEndpointRouteBuilder routes) {
        ArgumentNullException.ThrowIfNull(routes);
        var options = routes.ServiceProvider.GetRequiredService<DownloadOptions>();
        return routes.MapGet($"{options.Path}/{{id}}", (string id, HttpContext ctx, DownloadStore store) => Serve(id, ctx, store)).AllowAnonymous();
    }

    public static IResult Serve(string id, HttpContext ctx, DownloadStore store) {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(store);
        if (store.Take(id) is not { } item) return Results.NotFound();
        ctx.Response.Headers.CacheControl = "no-store";
        return Results.Bytes(item.Bytes, item.ContentType, item.FileName);
    }
}
