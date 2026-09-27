using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.Fallback;
using EggIdentity.Fleet;

namespace EggIdentity.Host;

internal static class FallbackRoutes {
    private const string PlainText = "text/plain";

    public static void Map(WebApplication app, HostConfig config) {
        var fleet = app.Services.GetRequiredService<DeployService>();

        app.MapGet("/fallback/{name}", async (string name, HttpContext ctx, RevocationStore revocations, CancellationToken ct) => {
            if (await fleet.FindAppAsync(name, ct) is not { } suiteApp) return Results.NotFound();
            var isAdmin = await IsAdminAsync(ctx, config, revocations, ct);
            var branding = new FallbackBranding(suiteApp.Label, FallbackDefaults.Tokens);
            var logsUrl = $"/logs/{Uri.EscapeDataString(suiteApp.Name)}/tail";
            return Results.Content(FallbackPages.RenderDown(branding, isAdmin, logsUrl), "text/html", null, StatusCodes.Status503ServiceUnavailable);
        });

        app.MapGet("/logs/{name}/tail", async (string name, int? lines, HttpContext ctx, RevocationStore revocations, CancellationToken ct) => {
            if (!await IsAdminAsync(ctx, config, revocations, ct)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try {
                var (text, refusal) = await fleet.LogsAsync(name, lines ?? 200, ct);
                return text is not null ? Results.Text(text, PlainText) : Results.Text(refusal, PlainText, null, StatusCodes.Status409Conflict);
            } catch (Exception e) when (e is not OperationCanceledException) {
                return Results.Text($"docker logs failed: {e.Message}", PlainText, null, StatusCodes.Status502BadGateway);
            }
        });
    }

    private static async Task<bool> IsAdminAsync(HttpContext ctx, HostConfig config, RevocationStore revocations, CancellationToken ct) =>
        config.SessionOptions is { } session
        && await ProfileAuth.TryGetPrincipalAsync(ctx, session, revocations.IsRevokedAsync, ct) is { } principal
        && principal.IsAtLeast(UserRole.Admin);
}
