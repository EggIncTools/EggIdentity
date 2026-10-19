using System.Reflection;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EggIdentity.UI;

public static class AppVersion {
    public const string Path = "/_app/version";

    public static string Current { get; } = Read(Assembly.GetEntryAssembly());

    internal static string Read(Assembly? assembly) =>
        assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

    public static IEndpointConventionBuilder MapAppVersion(this IEndpointRouteBuilder routes, string? version = null) {
        var served = version ?? Current;
        var display = BuildVersion.Describe(served, null);
        return routes.MapGet(Path, (HttpContext ctx) => {
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Json(new { version = served, display });
        }).AllowAnonymous();
    }
}
