using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Settings.Store;

namespace EggIdentity.Host;

public static class DiscordRegistrationRoutes {
    public static void Map(WebApplication app) =>
        app.MapGet("/identity/apps/{name}/discord", async (string name, SettingsCache cache, CancellationToken ct) => {
            var row = (await cache.GetAsync(ct)).Collection<SuiteApp>(SuiteApps.Key)
                .FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            return ToResponse(row) is { } registration ? Results.Ok(registration) : Results.NotFound();
        });

    public static DiscordRegistrationResponse? ToResponse(SuiteApp? app) =>
        app is { Enabled: true, RunsOwnBot: true, DiscordToken: { Length: > 0 } token, DiscordGuildId: { Length: > 0 } guild }
            ? new DiscordRegistrationResponse {
                Token = token,
                AppId = app.DiscordAppId,
                GuildId = guild,
                DashboardChannelId = app.DiscordDashboardChannelId,
            }
            : null;
}
