using System.Security.Cryptography;
using System.Text;
using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Fallback;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace EggIdentity.Agent;

internal static class Program {
    private const string PlainText = "text/plain";
    private static readonly TimeSpan EngineCallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultWatchInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultPullTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CatalogPollInterval = TimeSpan.FromSeconds(2);

    private static async Task<int> Main(string[] args) {
        if (SwapHelper.TryParseArgs(args, out var swap)) {
            var helperEngine = new DockerEngineClient(DockerEngineClient.CreateHttpClient(), EngineCallTimeout, () => DefaultPullTimeout);
            return await SwapHelper.RunAsync(helperEngine, swap, Console.Error);
        }

        var connString = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION");
        if (string.IsNullOrEmpty(connString)) {
            Console.Error.WriteLine("eggidentity-agent: IDENTITY_DB_CONNECTION is required; apps are read from the deploy.apps settings collection");
            return 1;
        }

        var registry = SettingsRegistry.Compose([AgentSettings.Provider, SessionSettings.Provider], [DeployApps.Provider]);
        await using var dataSource = NpgsqlDataSource.Create(connString);
        var store = new SettingsStore(dataSource, SecretProtector.FromEnvironment());
        using var cache = new SettingsCache(registry, store);

        SettingsSnapshot snapshot;
        try {
            await store.MigrateAsync();
            snapshot = await cache.GetAsync();
        } catch (Exception e) when (e is NpgsqlException or InvalidOperationException) {
            Console.Error.WriteLine($"eggidentity-agent: settings store unavailable: {e.Message}");
            return 1;
        }

        var port = snapshot.GetInt(AgentSettings.Port, 7777);
        var sessionOptions = SessionCookieOptions.FromEnvironment();
        var engine = new DockerEngineClient(
            DockerEngineClient.CreateHttpClient(), EngineCallTimeout, () => Live(cache, AgentSettings.PullTimeout, DefaultPullTimeout));
        var images = new RegistryClient(new HttpClient { Timeout = EngineCallTimeout });
        var events = new DeployEventRing();
        var service = new DeployService(AppCatalog.FromSnapshot(snapshot), engine, images, events);
        var runtime = new AgentRuntime(
            service, events, engine, PortainerConfig.FromSnapshot(snapshot), snapshot.GetString(AgentSettings.HookSecret));
        var app = Build(args, port, sessionOptions, runtime);

        var stopping = app.Lifetime.ApplicationStopping;
        _ = new SettingsChangeListener(dataSource, cache).RunAsync(stopping);
        _ = new AppCatalogSync(cache, service).RunAsync(CatalogPollInterval, stopping);
        _ = Task.Run(async () => {
            await service.ReapAsync(stopping);
            await service.RunPollLoopAsync(() => Live(cache, AgentSettings.WatchInterval, DefaultWatchInterval), stopping);
        }, stopping);

        var names = service.AppNames;
        Console.WriteLine($"eggidentity-agent: watching {names.Count} app(s) from deploy.apps: {string.Join(", ", names)}");
        Console.WriteLine($"eggidentity-agent: listening on :{port}");
        await app.RunAsync();
        return 0;
    }

    private static TimeSpan Live(SettingsCache cache, string key, TimeSpan fallback) {
        var value = cache.Current?.GetDuration(key);
        return value is { } duration && duration > TimeSpan.Zero ? duration : fallback;
    }

    private static WebApplication Build(string[] args, int port, SessionCookieOptions? sessionOptions, AgentRuntime runtime) {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddHttpClient();
        if (sessionOptions is not null) {
            builder.Services.AddAuthentication(EggIdentitySessionDefaults.Scheme)
                .AddEggIdentitySession(sessionOptions);
            builder.Services.AddAuthorization();
        }
        builder.WebHost.UseUrls($"http://*:{port}");

        var app = builder.Build();
        if (sessionOptions is not null) {
            app.UseAuthentication();
            app.UseAuthorization();
            MapAdminRoutes(app, runtime);
        }
        MapDeployRoutes(app, runtime);
        return app;
    }

    private static void MapAdminRoutes(WebApplication app, AgentRuntime runtime) {
        var service = runtime.Service;
        var engine = runtime.Engine;

        app.MapGet("/fallback/{appName}", (string appName, HttpContext ctx) => {
            if (!service.TryGetApp(appName, out _)) return Results.NotFound();
            var isAdmin = ctx.User.IsAtLeast(UserRole.Admin);
            var branding = new FallbackBranding(appName, FallbackDefaults.Tokens);
            var html = FallbackPages.RenderDown(branding, isAdmin, $"/logs/{appName}/tail");
            return Results.Content(html, "text/html");
        });

        app.MapGet("/logs/{appName}/tail", async (string appName, HttpContext ctx, int? lines) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (!service.TryGetApp(appName, out var cfg)) return Results.NotFound();
            var clampedLines = AgentRouteHelpers.ClampLogLines(lines);
            try {
                return Results.Text(await engine.LogsTailAsync(cfg.ContainerName, clampedLines, ctx.RequestAborted), PlainText);
            } catch (Exception e) when (e is not OperationCanceledException) {
                return Results.Text($"docker logs failed: {e.Message}", PlainText, null, StatusCodes.Status502BadGateway);
            }
        });

        app.MapGet("/status", (HttpContext ctx) =>
            ctx.User.IsAtLeast(UserRole.Admin) ? Results.Json(service.StatusAll()) : Results.Forbid());

        app.MapGet("/status/{appName}", (string appName, HttpContext ctx) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            var status = service.Status(appName);
            return status is null ? Results.NotFound() : Results.Json(status);
        });

        app.MapPost("/check/{appName}", async (string appName, HttpContext ctx) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (!service.TryGetApp(appName, out _)) return Results.NotFound();
            return Results.Json(await service.CheckAsync(appName, ctx.RequestAborted));
        });

        app.MapPost("/restart/{appName}", async (string appName, HttpContext ctx, IHostApplicationLifetime lifetime) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (!service.TryGetApp(appName, out _)) return Results.NotFound();
            return Results.Json(await service.RestartAsync(appName, lifetime.ApplicationStopping));
        });

        app.MapGet("/events", async (HttpContext ctx) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            var after = ServerSentEvents.ResolveAfter(ctx.Request.Headers["Last-Event-ID"], ctx.Request.Query["after"]);
            await ServerSentEvents.StreamAsync(ctx.Response, runtime.Events, after, ServerSentEvents.KeepaliveInterval, ctx.RequestAborted);
        });

        app.MapStackRoutes(runtime.Portainer);
        app.MapEnvRoutes(runtime);
    }

    private static void MapDeployRoutes(WebApplication app, AgentRuntime runtime) {
        var service = runtime.Service;

        app.MapPost("/deploy/{appName}", (string appName, HttpContext ctx, IHostApplicationLifetime lifetime) => {
            if (!service.TryGetApp(appName, out var cfg)) return Results.NotFound();
            if (!ctx.User.IsAtLeast(UserRole.Admin) && !BearerMatches(ctx.Request, cfg.DeploySecret))
                return Unauthorized();
            RunInBackground(() => service.DeployAsync(appName, "manual", lifetime.ApplicationStopping));
            return Results.Json(service.Status(appName), statusCode: StatusCodes.Status202Accepted);
        });

        app.MapPost("/hooks/image-pushed", (HttpContext ctx, DeployHookPayload payload, IHostApplicationLifetime lifetime) => {
            if (!BearerMatches(ctx.Request, runtime.HookSecret)) return Unauthorized();
            if (payload is null || string.IsNullOrWhiteSpace(payload.App)) return Results.BadRequest("app is required");
            if (!service.TryGetApp(payload.App, out var cfg)) return Results.NotFound();
            service.NoteRelease(cfg.Name, payload.Digest, payload.Revision, payload.Version);
            RunInBackground(async () => {
                var status = await service.CheckAsync(cfg.Name, lifetime.ApplicationStopping);
                if (cfg.AutoDeploy && status.UpdateAvailable)
                    await service.DeployAsync(cfg.Name, "hook", lifetime.ApplicationStopping);
            });
            return Results.Json(service.Status(cfg.Name), statusCode: StatusCodes.Status202Accepted);
        });
    }

    private static void RunInBackground(Func<Task> work) =>
        _ = Task.Run(async () => {
            try {
                await work();
            } catch (Exception e) {
                Console.Error.WriteLine($"eggidentity-agent: background work failed: {e}");
            }
        });

    private static IResult Unauthorized() =>
        Results.Text("unauthorized", PlainText, null, StatusCodes.Status401Unauthorized);

    private static bool BearerMatches(HttpRequest req, string? secret) {
        if (string.IsNullOrEmpty(secret)) return false;
        var header = req.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return false;
        var token = header["Bearer ".Length..].Trim();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret));
    }
}
