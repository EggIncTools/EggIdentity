using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace EggIdentity.Host;

internal static class LoginRoutes {
    private const string LinkPrefix = "link:";
    private const string HtmlContentType = "text/html";

    public static void Map(WebApplication app, HostConfig config, SponsorSyncService? sponsorSync) {
        var routes = app.MapGroup("/auth");

        routes.MapGet("/sources", (HttpContext ctx) => Sources(ctx, config));
        routes.MapGet("/icons/{provider}", (HttpContext ctx, string provider, IconCache icons) => Icon(ctx, provider, icons, config));
        routes.MapGet("/go/{provider}", (HttpContext ctx, string provider, OAuthStateStore states) => Go(ctx, provider, states, config));
        routes.MapGet("/callback", (HttpContext ctx, OAuthStateStore states, IdentityResolver resolver, LoginCodeStore codes, UserQueries users) =>
            Callback(ctx, states, resolver, codes, users, config, sponsorSync));
        routes.MapPost("/backchannel-logout", (HttpContext ctx, RevocationStore revocations) => BackchannelLogout(ctx, revocations, config));

        Func<HttpContext, Task<IResult>> logout = ctx => Logout(ctx, config);
        routes.MapGet("/logout", logout);
    }

    private static IResult Sources(HttpContext ctx, HostConfig config) {
        if (!config.LoginWidgetEnabled) return Results.NotFound();
        var returnUrl = ctx.Request.Query["returnUrl"].ToString();
        if (Program.ResolveApp(returnUrl, config.AppConfigs) is null) return Results.BadRequest("returnUrl not allowed");

        var mode = Program.ValidateMode(ctx.Request.Query["mode"].ToString());
        var sources = Program.KnownProviders.Select(provider => new LoginSourceResponse {
            Name = char.ToUpperInvariant(provider[0]) + provider[1..],
            IconUrl = $"/auth/icons/{provider}",
            Url = $"/auth/go/{provider}?returnUrl={Uri.EscapeDataString(returnUrl)}&mode={Uri.EscapeDataString(mode)}",
        }).ToList();
        return Results.Ok(new LoginSourcesResponse { Sources = sources });
    }

    private static async Task<IResult> Icon(HttpContext ctx, string provider, IconCache icons, HostConfig config) {
        if (!config.LoginWidgetEnabled) return Results.NotFound();
        if (!Program.KnownProviders.Contains(provider)) return Results.NotFound();

        var icon = await icons.GetAsync(provider, ctx.RequestAborted);
        if (icon is null)
            return Results.Redirect($"{config.AuthentikAuthority!.TrimEnd('/')}/static/authentik/sources/{provider}.svg");

        ctx.Response.Headers.CacheControl = "public, max-age=86400";
        return Results.Bytes(icon.Bytes, icon.ContentType);
    }

    private static async Task<IResult> Go(HttpContext ctx, string provider, OAuthStateStore states, HostConfig config) {
        if (!config.LoginWidgetEnabled) return Results.NotFound();
        var returnUrl = ctx.Request.Query["returnUrl"].ToString();
        var appConfig = Program.ResolveApp(returnUrl, config.AppConfigs);
        if (appConfig is null) return Results.BadRequest("returnUrl not allowed");

        var isLocal = provider == "local";
        if (isLocal && !Program.IsValidLocalKey(config.LocalLoginKey, ctx.Request.Headers["X-Local-Login-Key"]))
            return Results.NotFound();
        if (!isLocal && !Program.KnownProviders.Contains(provider))
            return Results.BadRequest("unknown provider");

        var mode = Program.ValidateMode(ctx.Request.Query["mode"].ToString());
        var (query, state, verifier) = appConfig.OAuth.BuildAuthParams();
        await states.SaveAsync(state, verifier, returnUrl, mode, ctx.RequestAborted);

        var authorizeUrl = $"/application/o/authorize/?{query}";
        return Results.Redirect(Program.BuildFlowUrl(appConfig.OAuth.Authority, provider, authorizeUrl));
    }

    private static async Task<IResult> Callback(
        HttpContext ctx, OAuthStateStore states, IdentityResolver resolver, LoginCodeStore codes, UserQueries users,
        HostConfig config, SponsorSyncService? sponsorSync) {
        if (!config.LoginWidgetEnabled) return Results.NotFound();
        var code = ctx.Request.Query["code"].ToString();
        var state = ctx.Request.Query["state"].ToString();
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Results.BadRequest("missing code or state");

        var saved = await states.ConsumeAsync(state, ctx.RequestAborted);
        if (saved is null)
            return Results.BadRequest("unknown or expired state");

        var appConfig = Program.ResolveApp(saved.ReturnUrl, config.AppConfigs);
        if (appConfig is null)
            return Results.BadRequest("returnUrl not allowed");

        string loginCode;
        try {
            var token = await appConfig.OAuth.HandleCallbackAsync(code, saved.CodeVerifier, ctx.RequestAborted);

            if (saved.Mode.StartsWith(LinkPrefix, StringComparison.Ordinal))
                return await CompleteLinkAsync(ctx, resolver, saved, token, sponsorSync);

            loginCode = await CompleteLoginAsync(ctx, resolver, codes, users, token, config, sponsorSync);
        } catch (Exception exc) {
            return CallbackFailure(exc, saved, appConfig);
        }

        if (saved.Mode == "redirect")
            return Results.Redirect(Program.BuildRedirectCallbackUrl(saved.ReturnUrl, code: loginCode, error: null));

        var payloadJson = JsonSerializer.Serialize(new { source = "eggidentity-auth", code = loginCode });
        return Results.Content(PostMessageHtml(payloadJson, appConfig.Origin, saved.Mode), HtmlContentType);
    }

    private static async Task<IResult> CompleteLinkAsync(
        HttpContext ctx, IdentityResolver resolver, OAuthState saved, AuthentikTokenResult token, SponsorSyncService? sponsorSync) {
        var (targetUserId, requestedProvider) = Program.ParseLinkMode(saved.Mode);
        var linkOutcome = await resolver.TryLinkAsync(
            targetUserId, "authentik", token.Sub, token.DiscordId, token.Username, token.Avatar, ctx.RequestAborted);
        var sourceOutcomes = await resolver.SyncSourceIdentitiesAsync(targetUserId, token.PerSourceIds, ctx.RequestAborted);
        var linkFlag = Program.ComputeLinkFlag(requestedProvider, linkOutcome, sourceOutcomes);

        if (sponsorSync is not null && requestedProvider == "github" && linkFlag == "linked=ok") {
            try {
                await sponsorSync.SyncAsync(targetUserId, ctx.RequestAborted);
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                Console.Error.WriteLine($"sponsor sync failed for {targetUserId}: {exc.Message}");
            }
        }

        return Results.Redirect(Program.AppendQuery(saved.ReturnUrl, linkFlag));
    }

    private static async Task<string> CompleteLoginAsync(
        HttpContext ctx, IdentityResolver resolver, LoginCodeStore codes, UserQueries users,
        AuthentikTokenResult token, HostConfig config, SponsorSyncService? sponsorSync) {
        var resolved = await resolver.ResolveAsync(
            "authentik", token.Sub, token.DiscordId, token.Username, token.Avatar, ctx.RequestAborted);
        await Program.TrySyncSourceIdentitiesAsync(resolver, resolved.UserId, token, ctx.RequestAborted);
        var loginCode = await codes.IssueAsync(resolved.UserId, resolved.IsNew, ctx.RequestAborted);

        if (sponsorSync is not null) {
            try {
                await sponsorSync.ReconcileRoleAsync(resolved.UserId, ctx.RequestAborted);
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                Console.Error.WriteLine($"sponsor reconcile failed for {resolved.UserId}: {exc.Message}");
            }
        }

        if (config.SessionOptions is { } sessionOptions)
            await IssueSessionAsync(ctx, users, resolved, token, sessionOptions);

        return loginCode;
    }

    private static async Task IssueSessionAsync(
        HttpContext ctx, UserQueries users, ResolveResult resolved, AuthentikTokenResult token,
        SessionCookieOptions sessionOptions) {
        var issuedAt = DateTimeOffset.UtcNow;
        var user = await users.GetAsync(resolved.UserId, ctx.RequestAborted);
        SessionIssuer.IssueCookie(ctx.Response, sessionOptions, new SessionUser(
            UserId: resolved.UserId.ToString(),
            Sid: token.Sid,
            Role: resolved.Role,
            Name: user?.Username ?? token.Username,
            Avatar: user?.Avatar ?? token.Avatar,
            DiscordId: user?.DiscordId ?? resolved.DiscordId),
            issuedAt);

        if (string.IsNullOrEmpty(token.IdToken)) return;

        ctx.Response.Cookies.Append(Program.IdHintCookie, token.IdToken, new CookieOptions {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/auth",
            Expires = issuedAt + sessionOptions.Ttl,
        });
    }

    private static IResult CallbackFailure(Exception exc, OAuthState saved, AppAuthConfig appConfig) {
        Console.Error.WriteLine($"{saved.Mode}: {exc}");

        if (saved.Mode.StartsWith(LinkPrefix, StringComparison.Ordinal))
            return Results.Redirect(Program.AppendQuery(saved.ReturnUrl, "linkError=1"));

        if (saved.Mode == "redirect")
            return Results.Redirect(Program.BuildRedirectCallbackUrl(saved.ReturnUrl, code: null, error: "login_failed"));

        var errorPayloadJson = JsonSerializer.Serialize(new { source = "eggidentity-auth", error = "login_failed" });
        return Results.Content(PostMessageHtml(errorPayloadJson, appConfig.Origin, saved.Mode), HtmlContentType);
    }

    private static string PostMessageHtml(string payloadJson, string origin, string mode) {
        var originJson = JsonSerializer.Serialize(origin);
        return $"""
            <!DOCTYPE html><html><body><script>
            var target = window.opener || window.parent;
            target && target.postMessage({payloadJson}, {originJson});
            {(mode == "inline" ? "" : "window.close();")}
            </script></body></html>
            """;
    }

    private static async Task<IResult> BackchannelLogout(HttpContext ctx, RevocationStore revocations, HostConfig config) {
        if (!config.LoginWidgetEnabled) return Results.NotFound();
        var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
        var logoutToken = form["logout_token"].ToString();
        if (string.IsNullOrWhiteSpace(logoutToken))
            return Results.BadRequest();

        var configManager = ctx.RequestServices.GetRequiredService<ConfigurationManager<OpenIdConnectConfiguration>>();
        OpenIdConnectConfiguration discovery;
        try {
            discovery = await configManager.GetConfigurationAsync(ctx.RequestAborted);
        } catch (Exception exc) {
            Console.Error.WriteLine($"backchannel-logout: {exc}");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var validationParams = new TokenValidationParameters {
            ValidIssuer = discovery.Issuer,
            IssuerSigningKeys = discovery.SigningKeys,
            ValidateAudience = false,
            ValidateLifetime = true,
        };

        System.Security.Claims.ClaimsPrincipal principal;
        try {
            principal = new JwtSecurityTokenHandler().ValidateToken(logoutToken, validationParams, out _);
        } catch (Exception) {
            return Results.BadRequest();
        }

        if (principal.FindFirst("nonce") is not null)
            return Results.BadRequest();

        var events = principal.FindFirst("events")?.Value;
        if (string.IsNullOrEmpty(events) || !events.Contains("backchannel-logout"))
            return Results.BadRequest();

        var sid = principal.FindFirst("sid")?.Value;
        if (string.IsNullOrEmpty(sid))
            return Results.BadRequest();

        await revocations.RevokeAsync(sid, ctx.RequestAborted);
        return Results.Ok();
    }

    private static async Task<IResult> Logout(HttpContext ctx, HostConfig config) {
        var returnUrlRaw = ctx.Request.Query["returnUrl"].ToString();
        var returnUrl = Program.ResolveApp(returnUrlRaw, config.AppConfigs) is not null ? returnUrlRaw : null;

        var idTokenHint = ctx.Request.Cookies.TryGetValue(Program.IdHintCookie, out var hint) ? hint : null;

        if (config.SessionOptions is not null)
            SessionIssuer.ClearCookie(ctx.Response, config.SessionOptions);
        ctx.Response.Cookies.Delete(Program.IdHintCookie, new CookieOptions { Path = "/auth" });

        var configManager = ctx.RequestServices.GetService<ConfigurationManager<OpenIdConnectConfiguration>>();
        if (configManager is not null) {
            try {
                var discovery = await configManager.GetConfigurationAsync(ctx.RequestAborted);
                if (!string.IsNullOrEmpty(discovery.EndSessionEndpoint))
                    return Results.Redirect(Program.BuildEndSessionUrl(discovery.EndSessionEndpoint, idTokenHint, returnUrl));
            } catch (Exception) {
                return string.IsNullOrEmpty(returnUrl) ? Results.Ok() : Results.Redirect(returnUrl);
            }
        }

        return string.IsNullOrEmpty(returnUrl) ? Results.Ok() : Results.Redirect(returnUrl);
    }
}
