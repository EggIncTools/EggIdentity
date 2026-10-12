using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EggIdentity.Bot;

public static class BotAdminApi {
    public static RouteGroupBuilder MapBotAdminApi(this RouteGroupBuilder group, Func<IServiceProvider, BotConfigService?> resolve) {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(resolve);

        group.MapGet("/bot", async (HttpContext ctx, CancellationToken ct) =>
            resolve(ctx.RequestServices) is not { } admin ? Unconfigured() : Results.Ok(await admin.GetAsync(ct)))
            .WithMetadata(new AdminCapability(AdminCapabilities.Bot));

        group.MapPut("/bot", async (BotConfigInput body, HttpContext ctx, CancellationToken ct) =>
            resolve(ctx.RequestServices) is not { } admin ? Unconfigured() : Results.Ok(await admin.SaveAsync(body, ct)));

        return group;
    }

    private static IResult Unconfigured() =>
        Results.Json(
            new SaveResult(false, "this app's bot is not running", null),
            statusCode: StatusCodes.Status503ServiceUnavailable);
}
