using EggIdentity.Auth;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace EggIdentity.Host;

internal static class ProfileLinkRoutes {
    public static void Map(WebApplication app, HostConfig config) {
        var sessionOptions = config.SessionOptions!;
        var revocations = app.Services.GetRequiredService<RevocationStore>();

        ProfileRoutes.Map(app, sessionOptions, config.AvatarStorageDir!, revocations,
            app.Services.GetRequiredService<ProfileService>(),
            app.Services.GetRequiredService<UserQueries>());

        app.MapGet("/profile/link/{provider}/start", (HttpContext ctx, string provider, OAuthStateStore states) =>
            StartLink(ctx, provider, states, config, sessionOptions, revocations));

        app.MapGet("/auth/relink/{provider}", (HttpContext ctx, string provider, OAuthStateStore states) =>
            Relink(ctx, provider, states, config, sessionOptions, revocations));

        app.MapGet("/auth/relink/continue", (HttpContext ctx) => {
            var target = ctx.Request.Query["state"].ToString();
            return Program.IsAllowedRelinkTarget(target, config.AuthentikAuthority!, Program.KnownProviders)
                ? Results.Redirect(target)
                : Results.BadRequest("invalid relink target");
        });
    }

    private static async Task<IResult> StartLink(
        HttpContext ctx, string provider, OAuthStateStore states, HostConfig config,
        SessionCookieOptions sessionOptions, RevocationStore revocations) {
        var prepared = await PrepareLinkAsync(ctx, provider, states, config, sessionOptions, revocations);
        return prepared.Failure ?? Results.Redirect(prepared.FlowUrl!);
    }

    private static async Task<IResult> Relink(
        HttpContext ctx, string provider, OAuthStateStore states, HostConfig config,
        SessionCookieOptions sessionOptions, RevocationStore revocations) {
        var prepared = await PrepareLinkAsync(ctx, provider, states, config, sessionOptions, revocations);
        if (prepared.Failure is not null) return prepared.Failure;

        var linkApp = prepared.App!;
        var flowUrl = prepared.FlowUrl!;

        var idTokenHint = ctx.Request.Cookies.TryGetValue(Program.IdHintCookie, out var hint) ? hint : null;
        if (string.IsNullOrEmpty(idTokenHint) || AuthentikOAuth.ReadAudienceFromIdToken(idTokenHint) != linkApp.OAuth.ClientId)
            return Results.Redirect(flowUrl);

        var endSessionUrl = linkApp.EndSessionUrl;
        if (string.IsNullOrEmpty(endSessionUrl)) {
            var configManager = ctx.RequestServices.GetService<ConfigurationManager<OpenIdConnectConfiguration>>();
            if (configManager is null) return Results.Redirect(flowUrl);
            try {
                var discovery = await configManager.GetConfigurationAsync(ctx.RequestAborted);
                endSessionUrl = discovery.EndSessionEndpoint;
            } catch (Exception) {
                return Results.Redirect(flowUrl);
            }
            if (string.IsNullOrEmpty(endSessionUrl)) return Results.Redirect(flowUrl);
        }

        var continueUrl = Program.BuildRelinkContinueUrl(linkApp.OAuth.CallbackUrl);
        return Results.Redirect(Program.BuildRelinkLogoutUrl(endSessionUrl, idTokenHint, continueUrl, flowUrl));
    }

    private static async Task<PreparedLink> PrepareLinkAsync(
        HttpContext ctx, string provider, OAuthStateStore states, HostConfig config,
        SessionCookieOptions sessionOptions, RevocationStore revocations) {
        var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions, revocations.IsRevokedAsync, ctx.RequestAborted);
        if (userId is null) return new PreparedLink(Results.Unauthorized(), null, null);

        var returnUrl = ctx.Request.Query["returnUrl"].ToString();
        var linkApp = Program.ResolveApp(returnUrl, config.AppConfigs);
        if (linkApp is null) return new PreparedLink(Results.BadRequest("returnUrl not allowed"), null, null);
        if (!Program.KnownProviders.Contains(provider)) return new PreparedLink(Results.BadRequest("unknown provider"), null, null);

        var (query, state, verifier) = linkApp.OAuth.BuildAuthParams();
        await states.SaveAsync(state, verifier, returnUrl, $"link:{userId}:{provider}", ctx.RequestAborted);

        var authorizeUrl = $"/application/o/authorize/?{query}";
        return new PreparedLink(null, linkApp, Program.BuildFlowUrl(linkApp.OAuth.Authority, provider, authorizeUrl));
    }

    private sealed record PreparedLink(IResult? Failure, AppAuthConfig? App, string? FlowUrl);
}
