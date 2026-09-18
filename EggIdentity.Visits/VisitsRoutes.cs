using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Visits;

public static class VisitsRoutes {
    private const int DefaultDays = 30;
    private const int MaxDays = 365;

    public static IEndpointConventionBuilder MapEggIdentityVisits(this IEndpointRouteBuilder routes) {
        ArgumentNullException.ThrowIfNull(routes);
        var options = routes.ServiceProvider.GetRequiredService<VisitsOptions>();
        return routes.MapPost(options.BeaconPath, (HttpContext ctx, VisitorHasher hasher, VisitTracker tracker) =>
            VisitBeacon.HandleAsync(ctx, options, hasher, tracker)).AllowAnonymous();
    }

    public static RouteGroupBuilder MapEggIdentityVisitsAdminApi(this RouteGroupBuilder group) {
        ArgumentNullException.ThrowIfNull(group);
        group.MapGet("/visits", async (int? days, VisitsStore store, VisitsOptions options, CancellationToken ct) =>
            Results.Ok(await store.QueryAsync(options.Site, Math.Clamp(days ?? DefaultDays, 1, MaxDays), ct)));
        return group;
    }
}
