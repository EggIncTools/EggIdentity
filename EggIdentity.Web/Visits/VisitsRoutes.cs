using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EggIdentity.Visits;

public static class VisitsRoutes {
    private const int DefaultDays = 30;
    private const int MaxDays = 365;

    public static RouteGroupBuilder MapEggIdentityVisitsAdminApi(this RouteGroupBuilder group) {
        ArgumentNullException.ThrowIfNull(group);
        group.MapGet("/visits", async (int? days, VisitsStore store, VisitsOptions options, CancellationToken ct) =>
            Results.Ok(await store.QueryAsync(options.Site, Math.Clamp(days ?? DefaultDays, 1, MaxDays), ct)))
            .WithMetadata(new AdminCapability(AdminCapabilities.Visits));
        return group;
    }
}
