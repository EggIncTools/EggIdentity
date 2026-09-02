using System.Security.Cryptography;
using System.Text;
using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.Fallback;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EggIdentity.Agent;

internal static class Program {
    private const string PlainText = "text/plain";

    private static int Main(string[] args) {
        var configDir = EnvOr("AGENT_CONFIG_DIR", "/etc/eggidentity/agents");
        var port = EnvOr("AGENT_PORT", "7777");
        var intervalText = EnvOr("AGENT_WATCH_INTERVAL", "1m");
        var notifySecret = Environment.GetEnvironmentVariable("DEPLOY_NOTIFY_SECRET") ?? "";
        var sessionOptions = SessionCookieOptions.FromEnvironment();

        if (!TryLoadRegistry(configDir, out var registry)) return 1;
        if (!TryParseInterval(intervalText, out var interval)) return 1;

        var orchestrator = new AgentOrchestrator(registry, interval, notifySecret);
        var app = Build(args, port, sessionOptions, registry, orchestrator);

        _ = orchestrator.RunAsync(app.Lifetime.ApplicationStopping);
        Console.WriteLine($"eggidentity-agent: watching {registry.Apps.Count} app(s) every {interval}: {string.Join(", ", registry.Apps.Keys)}");
        Console.WriteLine($"eggidentity-agent: listening on :{port}");
        app.Run();
        return 0;
    }

    private static WebApplication Build(
        string[] args, string port, SessionCookieOptions? sessionOptions,
        AgentRegistry registry, AgentOrchestrator orchestrator) {
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
            MapAdminRoutes(app, registry);
        }
        MapDeployRoutes(app, registry, orchestrator);
        return app;
    }

    private static void MapAdminRoutes(WebApplication app, AgentRegistry registry) {
        app.MapGet("/fallback/{appName}", (string appName, HttpContext ctx) => {
            if (!registry.Apps.ContainsKey(appName)) return Results.NotFound();
            var isAdmin = ctx.User.IsAtLeast(UserRole.Admin);
            var branding = new FallbackBranding(appName, FallbackDefaults.Tokens);
            var html = FallbackPages.RenderDown(branding, isAdmin, $"/logs/{appName}/tail");
            return Results.Content(html, "text/html");
        });

        app.MapGet("/logs/{appName}/tail", async (string appName, HttpContext ctx, int? lines) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.Forbid();
            if (!registry.Apps.ContainsKey(appName)) return Results.NotFound();
            var clampedLines = AgentRouteHelpers.ClampLogLines(lines);
            var output = await DockerLogReader.TailAsync(appName, clampedLines, ctx.RequestAborted);
            return Results.Text(output, PlainText);
        });

        app.MapStackRoutes(registry);
    }

    private static void MapDeployRoutes(WebApplication app, AgentRegistry registry, AgentOrchestrator orchestrator) {
        app.MapPost("/deploy/{appName}", async (string appName, HttpRequest req) => {
            if (!registry.Apps.TryGetValue(appName, out var cfg)) return Results.NotFound();
            if (!IsAuthorized(cfg, req)) return Unauthorized();
            var (res, ran) = await orchestrator.TryDeployAsync(appName);
            if (!ran) return Results.Text("deploy already in progress", PlainText, null, StatusCodes.Status409Conflict);
            return Results.Json(res);
        });

        app.MapPost("/deploy/{appName}/fast", async (string appName, HttpRequest req) => {
            if (!registry.Apps.TryGetValue(appName, out var cfg)) return Results.NotFound();
            if (!IsAuthorized(cfg, req)) return Unauthorized();
            if (!orchestrator.HasFastPipeline(appName))
                return Results.Text($"fast deploy not configured for {appName}", PlainText, null, StatusCodes.Status400BadRequest);
            var (res, ran) = await orchestrator.TryDeployFastAsync(appName);
            if (!ran) return Results.Text("deploy already in progress", PlainText, null, StatusCodes.Status409Conflict);
            return Results.Json(res);
        });
    }

    private static IResult Unauthorized() =>
        Results.Text("unauthorized", PlainText, null, StatusCodes.Status401Unauthorized);

    private static bool IsAuthorized(AgentConfig cfg, HttpRequest req) {
        var secret = Environment.GetEnvironmentVariable(cfg.SecretEnv) ?? "";
        var token = (req.Headers.Authorization.ToString() ?? "").Replace("Bearer ", "");
        return secret != "" && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret));
    }

    private static bool TryLoadRegistry(string configDir, out AgentRegistry registry) {
        try {
            registry = AgentRegistry.LoadFromDir(configDir);
            return true;
        } catch (Exception e) {
            Console.Error.WriteLine($"eggidentity-agent: load config dir: {e.Message}");
            registry = null!;
            return false;
        }
    }

    private static bool TryParseInterval(string text, out TimeSpan interval) {
        try {
            interval = AgentConfig.ParseDuration(text);
            return true;
        } catch (Exception e) {
            Console.Error.WriteLine($"eggidentity-agent: AGENT_WATCH_INTERVAL: {e.Message}");
            interval = default;
            return false;
        }
    }

    private static string EnvOr(string name, string fallback) {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }
}
