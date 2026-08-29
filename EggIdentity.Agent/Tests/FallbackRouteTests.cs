using System.Net;
using System.Security.Claims;
using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace EggIdentity.Agent.Tests;

public class FallbackRouteTests {
    [Fact]
    public async Task FallbackRoute_UnknownApp_ReturnsNotFound() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        var apps = new Dictionary<string, string> { ["eggledger"] = "eggledger" };
        app.MapGet("/fallback/{appName}", (string appName) =>
            apps.ContainsKey(appName)
                ? Results.Content(EggIdentity.Fallback.FallbackPages.RenderDown(
                    new EggIdentity.Fallback.FallbackBranding(appName, new Dictionary<string, string>()),
                    showAdminLink: false, logsUrl: $"/logs/{appName}/tail"), "text/html")
                : Results.NotFound());

        await app.StartAsync();
        await using var _ = app;
        var client = app.GetTestClient();

        var resp = await client.GetAsync("/fallback/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task FallbackRoute_KnownApp_ReturnsDownPage() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        var apps = new Dictionary<string, string> { ["eggledger"] = "eggledger" };
        app.MapGet("/fallback/{appName}", (string appName) =>
            apps.ContainsKey(appName)
                ? Results.Content(EggIdentity.Fallback.FallbackPages.RenderDown(
                    new EggIdentity.Fallback.FallbackBranding(appName, new Dictionary<string, string>()),
                    showAdminLink: false, logsUrl: $"/logs/{appName}/tail"), "text/html")
                : Results.NotFound());

        await app.StartAsync();
        await using var _ = app;
        var client = app.GetTestClient();

        var resp = await client.GetAsync("/fallback/eggledger");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("eggledger", body);
    }

    [Fact]
    public async Task LogsRoute_NonAdmin_GetsSameStatusRegardlessOfAppValidity() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        app.Use(async (ctx, next) => {
            var roleHeader = ctx.Request.Headers["X-Test-Role"].ToString();
            if (roleHeader != "")
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(SessionClaims.Role, roleHeader)]));
            await next();
        });

        var apps = new Dictionary<string, string> { ["eggledger"] = "eggledger" };
        app.MapGet("/logs/{appName}/tail", (string appName, HttpContext ctx, int? lines) => {
            if (!ctx.User.IsAtLeast(UserRole.Admin)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!apps.ContainsKey(appName)) return Results.NotFound();
            return Results.Text("ok", "text/plain");
        });

        await app.StartAsync();
        await using var _ = app;
        var client = app.GetTestClient();

        var knownResp = await client.GetAsync("/logs/eggledger/tail");
        var unknownResp = await client.GetAsync("/logs/does-not-exist/tail");

        Assert.Equal(HttpStatusCode.Forbidden, knownResp.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unknownResp.StatusCode);
    }
}
