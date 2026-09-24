using System.Collections.Concurrent;
using EggIdentity.Contract;

namespace EggIdentity.Host;

internal static class BrandRoutes {
    private const string CacheControl = "public, max-age=86400";
    private const string PngContentType = "image/png";
    private const string IcoContentType = "image/x-icon";

    private static readonly ConcurrentDictionary<string, byte[]> Cache = new(StringComparer.Ordinal);

    public static void Map(WebApplication app) {
        var routes = app.MapGroup("/brand");

        routes.MapGet("/manifest.json", (HttpContext ctx) => {
            ctx.Response.Headers.CacheControl = CacheControl;
            return Results.Json(Brands.All, contentType: "application/json");
        });

        routes.MapGet("/{slug}/icon-{size:int}.png", (HttpContext ctx, string slug, int size, IWebHostEnvironment env) => {
            var brand = Brands.Find(slug);
            return brand is null || !brand.Has(BrandFamily.Icon) || !Brands.IconSizes.Contains(size)
                ? Results.NotFound()
                : Serve(ctx, env, Brands.IconPath(brand.Slug, size), PngContentType);
        });

        routes.MapGet("/{slug}/lockup.png", (HttpContext ctx, string slug, IWebHostEnvironment env) => {
            var brand = Brands.Find(slug);
            return brand is null || !brand.Has(BrandFamily.Lockup)
                ? Results.NotFound()
                : Serve(ctx, env, Brands.LockupPath(brand.Slug), PngContentType);
        });

        routes.MapGet("/{slug}/wordmark.png", (HttpContext ctx, string slug, IWebHostEnvironment env) => {
            var brand = Brands.Find(slug);
            return brand is null || !brand.Has(BrandFamily.Wordmark)
                ? Results.NotFound()
                : Serve(ctx, env, Brands.WordmarkPath(brand.Slug), PngContentType);
        });

        routes.MapGet("/{slug}/{name}.ico", (HttpContext ctx, string slug, string name, IWebHostEnvironment env) => {
            var brand = Brands.Find(slug);
            return brand is null || !brand.HasFavicon || !string.Equals(name, brand.Slug, StringComparison.Ordinal)
                ? Results.NotFound()
                : Serve(ctx, env, Brands.FaviconPath(brand.Slug), IcoContentType);
        });
    }

    private static IResult Serve(HttpContext ctx, IWebHostEnvironment env, string path, string contentType) {
        var bytes = Read(env, path);
        if (bytes is null) return Results.NotFound();

        ctx.Response.Headers.CacheControl = CacheControl;
        return Results.Bytes(bytes, contentType);
    }

    private static byte[]? Read(IWebHostEnvironment env, string path) {
        if (Cache.TryGetValue(path, out var cached)) return cached;

        var file = env.WebRootFileProvider.GetFileInfo(path);
        if (!file.Exists || file.IsDirectory) return null;

        using var stream = file.CreateReadStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();

        Cache[path] = bytes;
        return bytes;
    }
}
