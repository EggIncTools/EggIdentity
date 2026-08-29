using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Discord.WebSocket;
using EggIdentity;
using EggIdentity.Auth;
using EggIdentity.Bot;
using EggIdentity.Client;
using EggIdentity.Contract;
using EggIdentity.Db;
using EggIdentity.Fallback;
using EggIdentity.Host;
using EggIdentity.Host.Components;
using EggIdentity.Models;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var connString = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION")
    ?? throw new InvalidOperationException("IDENTITY_DB_CONNECTION is required");
var apiSecret = Environment.GetEnvironmentVariable("IDENTITY_API_SECRET")
    ?? throw new InvalidOperationException("IDENTITY_API_SECRET is required");
var port = Environment.GetEnvironmentVariable("IDENTITY_API_PORT") ?? "8090";
var adminIds = Environment.GetEnvironmentVariable("IDENTITY_ADMIN_DISCORD_IDS");
var sweepIntervalMinutes = int.TryParse(Environment.GetEnvironmentVariable("IDENTITY_LOGIN_SWEEP_INTERVAL_MINUTES"), out var m) ? m : 10;

var localLoginKey = Environment.GetEnvironmentVariable("EGGIDENTITY_LOCAL_KEY");
var authentikAuthority = Environment.GetEnvironmentVariable("AUTHENTIK_AUTHORITY");
var authentikAppsDir = Environment.GetEnvironmentVariable("AUTHENTIK_APPS_DIR");
var loginWidgetEnabled = !string.IsNullOrEmpty(authentikAuthority) && !string.IsNullOrEmpty(authentikAppsDir);
var appConfigs = loginWidgetEnabled
    ? AppAuthConfigLoader.LoadFromDirectory(authentikAppsDir!, authentikAuthority!)
    : [];
var sessionOptions = SessionCookieOptions.FromEnvironment();
var avatarStorageDir = Environment.GetEnvironmentVariable("AVATAR_STORAGE_DIR");
var profileEnabled = loginWidgetEnabled && sessionOptions is not null && !string.IsNullOrEmpty(avatarStorageDir);
var sponsorConfig = SponsorConfig.FromEnvironment();
var sponsorEnabled = sponsorConfig is not null && sessionOptions is not null;

var botConfigFilePath = Environment.GetEnvironmentVariable("EGGIDENTITY_BOT_CONFIG_FILE") ?? "/etc/eggidentity/bot.env";
var botEnabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISCORD_TOKEN"));
var adminEnabled = botEnabled && loginWidgetEnabled && sessionOptions is not null;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://*:{port}");

var dataSource = NpgsqlDataSource.Create(connString);
builder.Services.AddSingleton(dataSource);
builder.Services.AddSingleton(AdminAllowlist.FromConfig(adminIds));
builder.Services.AddSingleton<IdentityResolver>();
builder.Services.AddSingleton<RevocationStore>();
builder.Services.AddSingleton<UserQueries>();
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddSingleton<LoginCodeStore>();
builder.Services.AddSingleton<OAuthStateStore>();
builder.Services.AddHttpClient();
builder.Services.AddEggIdentityFallback(new FallbackBranding("EggIdentity", FallbackDefaults.Tokens));
if (sponsorConfig is not null) {
    builder.Services.AddSingleton(sponsorConfig);
    builder.Services.AddSingleton<GitHubSponsorStatusStore>();
    builder.Services.AddSingleton<IGitHubSponsorClient>(sp =>
        new GitHubSponsorClient(sp.GetRequiredService<IHttpClientFactory>(), sponsorConfig.GitHubPat, sponsorConfig.GitHubTarget));
    builder.Services.AddSingleton<IDiscordRoleClient>(sp =>
        new DiscordRoleClient(sp.GetRequiredService<IHttpClientFactory>(), sponsorConfig.DiscordBotToken));
    builder.Services.AddSingleton<SponsorSyncService>();
    builder.Services.AddSingleton<SupporterStatusService>();
}
if (loginWidgetEnabled)
    builder.Services.AddSingleton(sp => new IconCache(sp.GetRequiredService<IHttpClientFactory>(), authentikAuthority!));

if (loginWidgetEnabled) {
    builder.Services.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>(
        $"{authentikAuthority!.TrimEnd('/')}/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever()));
}

if (botEnabled) {
    builder.Services.AddSingleton(new BotHostedService(botConfigFilePath, connString));
    builder.Services.AddHostedService(sp => sp.GetRequiredService<BotHostedService>());
    builder.Services.AddScoped(sp => sp.GetRequiredService<BotHostedService>().Bot?.ConfigService!);
}

if (adminEnabled) {
    builder.Services.AddHttpClient<IdentityApiClient>(c => {
        c.BaseAddress = new Uri($"http://localhost:{port}");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiSecret);
    });
    builder.Services.AddAuthentication(EggIdentitySessionDefaults.Scheme)
        .AddEggIdentitySession(sessionOptions!);
    builder.Services.AddAuthorization();
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddRazorComponents().AddInteractiveServerComponents();
}

var app = builder.Build();
app.UseStaticFiles();

if (adminEnabled) {
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();
}
app.UseEggIdentityFallback();

await using (var conn = await dataSource.OpenConnectionAsync())
    await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));

var sweeper = new ExpiredRowSweeper(dataSource, TimeSpan.FromMinutes(sweepIntervalMinutes));
_ = sweeper.RunAsync(app.Lifetime.ApplicationStopping);

var sponsorSyncOrNull = sponsorEnabled ? app.Services.GetRequiredService<SponsorSyncService>() : null;

app.MapGet("/", () => Results.Content(LandingPage.Html, "text/html"));
app.MapGet("/privacy", () => Results.Content(LegalPages.Privacy, "text/html"));
app.MapGet("/terms", () => Results.Content(LegalPages.Terms, "text/html"));

var loginRoutes = app.MapGroup("/auth");

loginRoutes.MapGet("/sources", (HttpContext ctx) => {
    if (!loginWidgetEnabled) return Results.NotFound();
    var returnUrl = ctx.Request.Query["returnUrl"].ToString();
    var app = Program.ResolveApp(returnUrl, appConfigs);
    if (app is null) return Results.BadRequest("returnUrl not allowed");

    var mode = Program.ValidateMode(ctx.Request.Query["mode"].ToString());
    var sources = Program.KnownProviders.Select(provider => new LoginSourceResponse {
        Name = char.ToUpperInvariant(provider[0]) + provider[1..],
        IconUrl = $"/auth/icons/{provider}",
        Url = $"/auth/go/{provider}?returnUrl={Uri.EscapeDataString(returnUrl)}&mode={Uri.EscapeDataString(mode)}",
    }).ToList();
    return Results.Ok(new LoginSourcesResponse { Sources = sources });
});

loginRoutes.MapGet("/icons/{provider}", async (HttpContext ctx, string provider, IconCache icons) => {
    if (!loginWidgetEnabled) return Results.NotFound();
    if (!Program.KnownProviders.Contains(provider)) return Results.NotFound();

    var icon = await icons.GetAsync(provider, ctx.RequestAborted);
    if (icon is null)
        return Results.Redirect($"{authentikAuthority!.TrimEnd('/')}/static/authentik/sources/{provider}.svg");

    ctx.Response.Headers.CacheControl = "public, max-age=86400";
    return Results.Bytes(icon.Bytes, icon.ContentType);
});

loginRoutes.MapGet("/go/{provider}", async (HttpContext ctx, string provider, OAuthStateStore states) => {
    if (!loginWidgetEnabled) return Results.NotFound();
    var returnUrl = ctx.Request.Query["returnUrl"].ToString();
    var app = Program.ResolveApp(returnUrl, appConfigs);
    if (app is null) return Results.BadRequest("returnUrl not allowed");

    var isLocal = provider == "local";
    if (isLocal && !Program.IsValidLocalKey(localLoginKey, ctx.Request.Headers["X-Local-Login-Key"]))
        return Results.NotFound();
    if (!isLocal && !Program.KnownProviders.Contains(provider))
        return Results.BadRequest("unknown provider");

    var mode = Program.ValidateMode(ctx.Request.Query["mode"].ToString());
    var (query, state, verifier) = app.OAuth.BuildAuthParams();
    await states.SaveAsync(state, verifier, returnUrl, mode, ctx.RequestAborted);

    var authorizeUrl = $"/application/o/authorize/?{query}";
    var flowUrl = Program.BuildFlowUrl(app.OAuth.Authority, provider, authorizeUrl);
    return Results.Redirect(flowUrl);
});

loginRoutes.MapGet("/callback", async (HttpContext ctx, OAuthStateStore states, IdentityResolver resolver, LoginCodeStore codes, UserQueries users) => {
    if (!loginWidgetEnabled) return Results.NotFound();
    var code = ctx.Request.Query["code"].ToString();
    var state = ctx.Request.Query["state"].ToString();
    if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        return Results.BadRequest("missing code or state");

    var saved = await states.ConsumeAsync(state, ctx.RequestAborted);
    if (saved is null)
        return Results.BadRequest("unknown or expired state");

    var app = Program.ResolveApp(saved.ReturnUrl, appConfigs);
    if (app is null)
        return Results.BadRequest("returnUrl not allowed");

    string loginCode;
    try {
        var token = await app.OAuth.HandleCallbackAsync(code, saved.CodeVerifier, ctx.RequestAborted);

        if (saved.Mode.StartsWith("link:", StringComparison.Ordinal)) {
            var (targetUserId, requestedProvider) = Program.ParseLinkMode(saved.Mode);
            var linkOutcome = await resolver.TryLinkAsync(targetUserId, "authentik", token.Sub, token.DiscordId, token.Username, token.Avatar, ctx.RequestAborted);
            var sourceOutcomes = await resolver.SyncSourceIdentitiesAsync(targetUserId, token.PerSourceIds, ctx.RequestAborted);
            var linkFlag = Program.ComputeLinkFlag(requestedProvider, linkOutcome, sourceOutcomes);

            if (sponsorSyncOrNull is not null && requestedProvider == "github" && linkFlag == "linked=ok") {
                try {
                    await sponsorSyncOrNull.SyncAsync(targetUserId, ctx.RequestAborted);
                } catch (Exception exc) when (exc is not OperationCanceledException) {
                    Console.Error.WriteLine($"sponsor sync failed for {targetUserId}: {exc.Message}");
                }
            }

            return Results.Redirect(Program.AppendQuery(saved.ReturnUrl, linkFlag));
        }

        var resolved = await resolver.ResolveAsync("authentik", token.Sub, token.DiscordId, token.Username, token.Avatar, ctx.RequestAborted);
        await Program.TrySyncSourceIdentitiesAsync(resolver, resolved.UserId, token, ctx.RequestAborted);
        loginCode = await codes.IssueAsync(resolved.UserId, resolved.IsNew, ctx.RequestAborted);

        if (sponsorSyncOrNull is not null) {
            try {
                await sponsorSyncOrNull.ReconcileRoleAsync(resolved.UserId, ctx.RequestAborted);
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                Console.Error.WriteLine($"sponsor reconcile failed for {resolved.UserId}: {exc.Message}");
            }
        }

        if (sessionOptions is not null) {
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
            if (!string.IsNullOrEmpty(token.IdToken)) {
                ctx.Response.Cookies.Append(Program.IdHintCookie, token.IdToken, new CookieOptions {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/auth",
                    Expires = issuedAt + sessionOptions.Ttl,
                });
            }
        }
    } catch (Exception exc) {
        Console.Error.WriteLine($"{saved.Mode}: {exc}");

        if (saved.Mode.StartsWith("link:", StringComparison.Ordinal))
            return Results.Redirect(Program.AppendQuery(saved.ReturnUrl, "linkError=1"));

        if (saved.Mode == "redirect")
            return Results.Redirect(Program.BuildRedirectCallbackUrl(saved.ReturnUrl, code: null, error: "login_failed"));

        var errorPayloadJson = System.Text.Json.JsonSerializer.Serialize(new { source = "eggidentity-auth", error = "login_failed" });
        var errorOriginJson = System.Text.Json.JsonSerializer.Serialize(app.Origin);
        var errorHtml = $"""
            <!DOCTYPE html><html><body><script>
            var target = window.opener || window.parent;
            target && target.postMessage({errorPayloadJson}, {errorOriginJson});
            {(saved.Mode == "inline" ? "" : "window.close();")}
            </script></body></html>
            """;
        return Results.Content(errorHtml, "text/html");
    }

    if (saved.Mode == "redirect")
        return Results.Redirect(Program.BuildRedirectCallbackUrl(saved.ReturnUrl, code: loginCode, error: null));

    var payloadJson = System.Text.Json.JsonSerializer.Serialize(new { source = "eggidentity-auth", code = loginCode });
    var originJson = System.Text.Json.JsonSerializer.Serialize(app.Origin);
    var html = $"""
        <!DOCTYPE html><html><body><script>
        var target = window.opener || window.parent;
        target && target.postMessage({payloadJson}, {originJson});
        {(saved.Mode == "inline" ? "" : "window.close();")}
        </script></body></html>
        """;
    return Results.Content(html, "text/html");
});

loginRoutes.MapPost("/backchannel-logout", async (HttpContext ctx, RevocationStore revocations) => {
    if (!loginWidgetEnabled) return Results.NotFound();
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
});

loginRoutes.MapGet("/logout", async (HttpContext ctx) => {
    var returnUrlRaw = ctx.Request.Query["returnUrl"].ToString();
    var returnUrl = Program.ResolveApp(returnUrlRaw, appConfigs) is not null ? returnUrlRaw : null;

    var idTokenHint = ctx.Request.Cookies.TryGetValue(Program.IdHintCookie, out var hint) ? hint : null;

    if (sessionOptions is not null)
        SessionIssuer.ClearCookie(ctx.Response, sessionOptions);
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
});

if (profileEnabled) {
    ProfileRoutes.Map(app, sessionOptions!, avatarStorageDir!, app.Services.GetRequiredService<RevocationStore>(),
        app.Services.GetRequiredService<ProfileService>(), app.Services.GetRequiredService<UserQueries>());

    app.MapGet("/profile/link/{provider}/start", async (HttpContext ctx, string provider, OAuthStateStore states) => {
        var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions!, app.Services.GetRequiredService<RevocationStore>().IsRevokedAsync, ctx.RequestAborted);
        if (userId is null) return Results.Unauthorized();

        var returnUrl = ctx.Request.Query["returnUrl"].ToString();
        var linkApp = Program.ResolveApp(returnUrl, appConfigs);
        if (linkApp is null) return Results.BadRequest("returnUrl not allowed");
        if (!Program.KnownProviders.Contains(provider)) return Results.BadRequest("unknown provider");

        var (query, state, verifier) = linkApp.OAuth.BuildAuthParams();
        await states.SaveAsync(state, verifier, returnUrl, $"link:{userId}:{provider}", ctx.RequestAborted);

        var authorizeUrl = $"/application/o/authorize/?{query}";
        var flowUrl = Program.BuildFlowUrl(linkApp.OAuth.Authority, provider, authorizeUrl);
        return Results.Redirect(flowUrl);
    });

    app.MapGet("/auth/relink/{provider}", async (HttpContext ctx, string provider, OAuthStateStore states) => {
        var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions!, app.Services.GetRequiredService<RevocationStore>().IsRevokedAsync, ctx.RequestAborted);
        if (userId is null) return Results.Unauthorized();

        var returnUrl = ctx.Request.Query["returnUrl"].ToString();
        var linkApp = Program.ResolveApp(returnUrl, appConfigs);
        if (linkApp is null) return Results.BadRequest("returnUrl not allowed");
        if (!Program.KnownProviders.Contains(provider)) return Results.BadRequest("unknown provider");

        var (query, state, verifier) = linkApp.OAuth.BuildAuthParams();
        await states.SaveAsync(state, verifier, returnUrl, $"link:{userId}:{provider}", ctx.RequestAborted);

        var authorizeUrl = $"/application/o/authorize/?{query}";
        var flowUrl = Program.BuildFlowUrl(linkApp.OAuth.Authority, provider, authorizeUrl);

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
    });

    app.MapGet("/auth/relink/continue", (HttpContext ctx) => {
        var target = ctx.Request.Query["state"].ToString();
        return Program.IsAllowedRelinkTarget(target, authentikAuthority!, Program.KnownProviders)
            ? Results.Redirect(target)
            : Results.BadRequest("invalid relink target");
    });
}

if (sponsorEnabled) {
    var sponsorSync = sponsorSyncOrNull!;
    var sponsorStore = app.Services.GetRequiredService<GitHubSponsorStatusStore>();
    var sponsorRevocations = app.Services.GetRequiredService<RevocationStore>();

    app.MapPost("/profile/sponsor/refresh", async (HttpContext ctx) => {
        var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions!, sponsorRevocations.IsRevokedAsync, ctx.RequestAborted);
        if (userId is null) return Results.Unauthorized();

        var existing = await sponsorStore.GetAsync(userId.Value, ctx.RequestAborted);
        var now = DateTimeOffset.UtcNow;
        if (Program.ShouldThrottleSponsorRefresh(existing?.LastSyncedAt, now)) {
            ctx.Response.Headers.RetryAfter = "30";
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        EggIdentity.Models.GitHubSponsorStatus? status;
        try {
            status = await sponsorSync.SyncAsync(userId.Value, ctx.RequestAborted);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            Console.Error.WriteLine($"sponsor refresh failed for {userId}: {exc.Message}");
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
        if (status is null) return Results.BadRequest("no linked github account");

        return Results.Ok(new SponsorStatusResponse {
            IsSponsor = status.IsSponsor,
            LastSyncedAt = status.LastSyncedAt,
        });
    });

    app.MapPost("/profile/supporter/refresh", async (HttpContext ctx) => {
        var userId = await ProfileAuth.TryGetUserIdAsync(ctx, sessionOptions!, sponsorRevocations.IsRevokedAsync, ctx.RequestAborted);
        if (userId is null) return Results.Unauthorized();

        var supporterStatus = app.Services.GetRequiredService<SupporterStatusService>();
        bool? refreshed;
        try {
            refreshed = await supporterStatus.RefreshAsync(userId.Value, ctx.RequestAborted);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            Console.Error.WriteLine($"supporter refresh failed for {userId}: {exc.Message}");
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }
        if (refreshed is null) {
            ctx.Response.Headers.RetryAfter = "30";
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        return Results.Ok(new SupporterStatusResponse { IsSupporter = refreshed.Value });
    });

    app.MapPost("/webhooks/github/sponsorship", async (HttpContext ctx) => {
        using var bodyStream = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(bodyStream, ctx.RequestAborted);
        var bodyBytes = bodyStream.ToArray();
        var bodyText = System.Text.Encoding.UTF8.GetString(bodyBytes);

        var signature = ctx.Request.Headers["X-Hub-Signature-256"].ToString();
        if (!SponsorWebhook.VerifySignature(sponsorConfig!.GitHubWebhookSecret, bodyBytes, signature))
            return Results.Unauthorized();

        EggIdentity.Host.SponsorshipWebhookEvent? evt;
        try {
            evt = SponsorWebhook.ParsePayload(bodyText);
        } catch (Exception exc) when (exc is System.Text.Json.JsonException or InvalidOperationException) {
            return Results.Ok();
        }
        if (evt is null) return Results.Ok();

        var isSponsor = SponsorWebhook.ResolveIsSponsor(evt.Action);
        if (isSponsor is null) return Results.Ok();

        await sponsorSync.ApplyWebhookEventAsync(evt.SponsorSubject, isSponsor.Value, ctx.RequestAborted);
        return Results.Ok();
    });
}

app.Use(async (ctx, next) => {
    if (ctx.Request.Path == "/" || ctx.Request.Path.StartsWithSegments("/auth") || ctx.Request.Path.StartsWithSegments("/eggidentity-login.js")
        || ctx.Request.Path.StartsWithSegments("/profile") || ctx.Request.Path.StartsWithSegments("/avatars")
        || ctx.Request.Path.StartsWithSegments("/webhooks") || ctx.Request.Path.StartsWithSegments("/admin")
        || ctx.Request.Path.StartsWithSegments("/privacy") || ctx.Request.Path.StartsWithSegments("/terms")
        || ctx.Request.Path.StartsWithSegments("/_framework") || ctx.Request.Path.StartsWithSegments("/_blazor")
        || ctx.Request.Path.StartsWithSegments("/_content")) {
        await next();
        return;
    }
    var auth = ctx.Request.Headers.Authorization.ToString();
    if (auth != $"Bearer {apiSecret}") {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsync("unauthorized");
        return;
    }
    await next();
});

app.MapPost("/identity/resolve", async (IdentityResolveRequest req, IdentityResolver resolver, CancellationToken ct) => {
    var result = await resolver.ResolveAsync(req.Provider, req.Subject, req.DiscordId, req.Username, req.Avatar, ct);
    return Results.Ok(new IdentityResolveResponse {
        UserId = result.UserId,
        Role = result.Role,
        DiscordId = result.DiscordId,
        IsNew = result.IsNew,
    });
});

app.MapGet("/identity/{userId:guid}", async (Guid userId, UserQueries users, CancellationToken ct) => {
    var user = await users.GetAsync(userId, ct);
    if (user is null) return Results.NotFound();
    return Results.Ok(ToResponse(user));
});

if (sponsorConfig is not null) {
    app.MapGet("/identity/{userId:guid}/sponsor", async (Guid userId, GitHubSponsorStatusStore store, CancellationToken ct) => {
        var status = await store.GetAsync(userId, ct);
        return Results.Ok(new SponsorStatusResponse {
            IsSponsor = status?.IsSponsor ?? false,
            LastSyncedAt = status?.LastSyncedAt,
        });
    });

    app.MapGet("/identity/{userId:guid}/supporter", async (Guid userId, SupporterStatusService supporterStatus, CancellationToken ct) => {
        var isSupporter = await supporterStatus.IsSupporterAsync(userId, ct);
        return Results.Ok(new SupporterStatusResponse { IsSupporter = isSupporter });
    });
}

app.MapPost("/identity/revoke-session", async (RevokeSessionRequest req, RevocationStore store, CancellationToken ct) => {
    await store.RevokeAsync(req.Sid, ct);
    return Results.NoContent();
});

app.MapGet("/identity/sessions/{sid}/revoked", async (string sid, RevocationStore store, CancellationToken ct) =>
    Results.Ok(await store.IsRevokedAsync(sid, ct)));

app.MapPost("/identity/merge", async (MergeUsersRequest req, IdentityResolver resolver, CancellationToken ct) => {
    var winner = await resolver.MergeAsync(req.KeepUserId, req.MergeUserId, ct);
    return Results.Ok(new { userId = winner });
});

app.MapGet("/identity/admin/users", async (UserQueries users, CancellationToken ct) => {
    var list = await users.ListAsync(ct);
    var providers = await users.ListProvidersAsync(ct);
    return Results.Ok(list.Select(u => ToResponse(u, providers.GetValueOrDefault(u.UserId) ?? [])));
});

app.MapPost("/identity/{userId:guid}/role", async (Guid userId, SetRoleRequest req, UserQueries users, CancellationToken ct) => {
    var ok = await users.SetRoleAsync(userId, UserRoles.Parse(req.Role), ct);
    return ok ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/identity/redeem", async (RedeemLoginCodeRequest req, LoginCodeStore codes, UserQueries users, CancellationToken ct) => {
    var redeemed = await codes.RedeemAsync(req.Code, ct);
    if (redeemed is null) return Results.NotFound();
    var user = await users.GetAsync(redeemed.UserId, ct);
    if (user is null) return Results.NotFound();
    return Results.Ok(new RedeemLoginCodeResponse {
        UserId = user.UserId,
        DiscordId = user.DiscordId,
        Username = user.Username,
        Avatar = user.Avatar,
        Role = user.Role,
        IsNew = redeemed.IsNew,
    });
});

if (adminEnabled)
    app.MapRazorComponents<AppHost>().AddInteractiveServerRenderMode();

app.Run();

static IdentityUserResponse ToResponse(User u, List<string>? providers = null) => new() {
    UserId = u.UserId,
    DiscordId = u.DiscordId,
    Username = u.Username,
    Avatar = u.Avatar,
    Role = u.Role,
    Providers = providers ?? [],
    CreatedAt = u.CreatedAt,
    LastLoginAt = u.LastLoginAt,
};

public partial class Program {
    public const string IdHintCookie = "eggidentity_idhint";

    public static readonly string[] KnownProviders = ["discord", "google", "microsoft", "github"];

    public static bool IsValidLocalKey(string? configured, string? presented) {
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(presented)) return false;
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        return CryptographicOperations.FixedTimeEquals(configuredHash, presentedHash);
    }

    public static bool ShouldThrottleSponsorRefresh(DateTimeOffset? lastSyncedAt, DateTimeOffset now) =>
        lastSyncedAt is { } last && now - last < TimeSpan.FromSeconds(30);

    public static async Task TrySyncSourceIdentitiesAsync(
        IdentityResolver resolver, Guid userId, AuthentikTokenResult token, CancellationToken ct) {
        try {
            await resolver.SyncSourceIdentitiesAsync(userId, token.PerSourceIds, ct);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            Console.Error.WriteLine($"source identity sync failed for {userId}: {exc.Message}");
        }
    }

    public static (Guid UserId, string? Provider) ParseLinkMode(string mode) {
        var body = mode["link:".Length..];
        var separator = body.IndexOf(':');
        return separator < 0
            ? (Guid.Parse(body), null)
            : (Guid.Parse(body[..separator]), body[(separator + 1)..]);
    }

    public static string ComputeLinkFlag(string? requestedProvider, LinkOutcome authentikOutcome, IReadOnlyList<(string Provider, LinkOutcome Outcome)> sourceOutcomes) {
        if (requestedProvider is not null) {
            var requested = sourceOutcomes.FirstOrDefault(o => o.Provider == requestedProvider);
            if (requested.Provider is not null) {
                if (requested.Outcome.Conflict) return $"linkConflict={requestedProvider}";
                if (requested.Outcome.NotAvailable) return $"linkUnavailable={requestedProvider}";
                if (requested.Outcome.AlreadyLinked) return $"linkRejected={requestedProvider}";
                if (requested.Outcome.Linked) return "linked=ok";
            }
            return "linkError=1";
        }

        var conflict = sourceOutcomes.FirstOrDefault(o => o.Outcome.Conflict);
        if (conflict.Provider is not null) return $"linkConflict={conflict.Provider}";
        if (authentikOutcome.Conflict) return "linkConflict=authentik";
        var rejected = sourceOutcomes.FirstOrDefault(o => o.Outcome.AlreadyLinked);
        if (rejected.Provider is not null) return $"linkRejected={rejected.Provider}";
        if (authentikOutcome.AlreadyLinked) return "linkRejected=authentik";
        if (authentikOutcome.Linked || sourceOutcomes.Any(o => o.Outcome.Linked)) return "linked=ok";
        return "linkError=1";
    }

    public static string BuildEndSessionUrl(string endSessionEndpoint, string? idTokenHint, string? returnUrl) {
        var url = endSessionEndpoint;
        if (!string.IsNullOrEmpty(idTokenHint))
            url = Append(url, $"id_token_hint={Uri.EscapeDataString(idTokenHint)}");
        if (!string.IsNullOrEmpty(returnUrl))
            url = Append(url, $"post_logout_redirect_uri={Uri.EscapeDataString(returnUrl)}");
        return url;

        static string Append(string target, string param) {
            var sep = target.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{target}{sep}{param}";
        }
    }

    public static string ValidateMode(string? raw) =>
        raw is "inline" or "redirect" ? raw : "popup";

    public static AppAuthConfig? ResolveApp(string returnUrl, Dictionary<string, AppAuthConfig> appConfigs) {
        if (string.IsNullOrEmpty(returnUrl) || !Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsed))
            return null;
        var origin = $"{parsed.Scheme}://{parsed.Authority}";
        return appConfigs.TryGetValue(origin, out var app) ? app : null;
    }

    public static string BuildFlowUrl(string authority, string provider, string authorizeUrl) {
        var flowSlug = $"{provider}-only-auth";
        return $"{authority}/if/flow/{flowSlug}/?next={Uri.EscapeDataString(authorizeUrl)}";
    }

    public static string BuildRelinkContinueUrl(string callbackUrl) =>
        callbackUrl.EndsWith("/callback", StringComparison.Ordinal)
            ? callbackUrl[..^"/callback".Length] + "/relink/continue"
            : callbackUrl.TrimEnd('/') + "/relink/continue";

    public static string BuildRelinkLogoutUrl(string endSessionUrl, string idTokenHint, string continueUrl, string flowUrl) {
        var url = Append(endSessionUrl, $"id_token_hint={Uri.EscapeDataString(idTokenHint)}");
        url = Append(url, $"post_logout_redirect_uri={Uri.EscapeDataString(continueUrl)}");
        url = Append(url, $"state={Uri.EscapeDataString(flowUrl)}");
        return url;

        static string Append(string target, string param) {
            var sep = target.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{target}{sep}{param}";
        }
    }

    public static bool IsAllowedRelinkTarget(string? target, string authority, string[] knownProviders) {
        if (string.IsNullOrEmpty(target)) return false;
        if (!Uri.TryCreate(target, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) return false;
        var trimmed = authority.TrimEnd('/');
        if (!knownProviders.Any(provider => target.StartsWith($"{trimmed}/if/flow/{provider}-only-auth/", StringComparison.Ordinal)))
            return false;

        var next = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(parsed.Query).TryGetValue("next", out var values)
            ? values.ToString()
            : null;
        return !string.IsNullOrEmpty(next) && next.StartsWith($"{trimmed}/application/o/authorize/", StringComparison.Ordinal);
    }

    public static string BuildRedirectCallbackUrl(string returnUrl, string? code, string? error) {
        var param = code is not null ? $"code={Uri.EscapeDataString(code)}" : $"error={Uri.EscapeDataString(error!)}";
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{returnUrl}{separator}{param}";
    }

    public static string AppendQuery(string returnUrl, string param) {
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{returnUrl}{separator}{param}";
    }
}
