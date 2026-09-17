using EggIdentity.Bot;

namespace EggIdentity.Host;

internal static class BotAdminRoutes {
    public static void Map(RouteGroupBuilder group) {
        group.MapGet("/bot", async (BotHostedService bot, CancellationToken ct) =>
            bot.Bot?.ConfigService is not { } admin ? Unconfigured() : Results.Ok(await admin.GetAsync(ct)));

        group.MapPut("/bot", async (BotConfigInput body, BotHostedService bot, CancellationToken ct) =>
            bot.Bot?.ConfigService is not { } admin ? Unconfigured() : Results.Ok(await admin.SaveAsync(body, ct)));
    }

    private static IResult Unconfigured() =>
        Results.Json(
            new SaveResult(false, "this app's bot is not running", null),
            statusCode: StatusCodes.Status503ServiceUnavailable);
}
