using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Settings.Store;

namespace EggIdentity.Host;

public static class SuiteToolRoutes {
    public static void Map(WebApplication app) =>
        app.MapGet("/suite/tools", async (SettingsCache cache, HttpContext ctx) => {
            var snapshot = await cache.GetAsync(ctx.RequestAborted);
            ctx.Response.Headers.CacheControl = "public, max-age=60";
            return Results.Ok(Project(snapshot.Collection<SuiteApp>(SuiteApps.Key)));
        }).AllowAnonymous();

    public static IReadOnlyList<SuiteTool> Project(IReadOnlyList<SuiteApp> apps) {
        ArgumentNullException.ThrowIfNull(apps);
        var tools = new List<SuiteTool>();
        foreach (var app in apps) {
            if (!app.Enabled || app.IsSubProd) continue;
            var slug = app.BrandSlug?.Trim();
            if (string.IsNullOrEmpty(slug) || Brands.Find(slug) is null) continue;
            if (!Uri.TryCreate(app.PublicUrl?.Trim(), UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps) continue;
            tools.Add(new SuiteTool { Slug = slug, Url = url.GetLeftPart(UriPartial.Authority), Live = app.Listed, Order = tools.Count });
        }
        return tools;
    }
}
