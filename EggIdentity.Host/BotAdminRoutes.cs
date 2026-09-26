using EggIdentity.Bot;
using EggIdentity.Contract;

namespace EggIdentity.Host;

internal static class BotAdminRoutes {
    public static void Map(RouteGroupBuilder group) {
        var capability = new AdminCapability(AdminCapabilities.Bot);

        group.MapGet("/bot", async (BotHostedService bot, CancellationToken ct) =>
            bot.Bot?.ConfigService is not { } admin ? Unconfigured() : Results.Ok(await admin.GetAsync(ct)))
            .WithMetadata(capability);

        group.MapPut("/bot", async (BotConfigInput body, BotHostedService bot, CancellationToken ct) =>
            bot.Bot?.ConfigService is not { } admin ? Unconfigured() : Results.Ok(await admin.SaveAsync(body, ct)));

        group.MapGet("/bot/{app}", async (string app, BotHostedService bot, SuiteBotApps served, CancellationToken ct) =>
            await ServedAsync(app, bot, served, ct) is not { } admin ? NotServed(app) : Results.Ok(await admin.GetAsync(ct)));

        group.MapPut("/bot/{app}", async (string app, BotConfigInput body, BotHostedService bot, SuiteBotApps served, CancellationToken ct) =>
            await ServedAsync(app, bot, served, ct) is not { } admin ? NotServed(app) : Results.Ok(await admin.SaveAsync(body, ct)));
    }

    private static async Task<BotConfigService?> ServedAsync(string app, BotHostedService bot, SuiteBotApps served, CancellationToken ct) {
        if (bot.Bot is not { } running) return null;
        if (string.Equals(app, HostServices.AppName, StringComparison.OrdinalIgnoreCase)) return running.ConfigService;
        var apps = await served.ServedAsync(ct);
        return apps.Any(a => string.Equals(a.Name, app, StringComparison.OrdinalIgnoreCase)) ? running.ConfigServiceFor(app) : null;
    }

    private static IResult Unconfigured() =>
        Results.Json(
            new SaveResult(false, "this app's bot is not running", null),
            statusCode: StatusCodes.Status503ServiceUnavailable);

    private static IResult NotServed(string app) =>
        Results.Json(
            new SaveResult(false, $"the suite bot is not running, or suite.apps row {app} does not set discord_bot to suite", null),
            statusCode: StatusCodes.Status404NotFound);
}
