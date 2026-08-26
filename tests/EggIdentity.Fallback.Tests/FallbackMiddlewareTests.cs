using System.Net;
using System.Security.Claims;
using EggIdentity.Auth;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EggIdentity.Fallback.Tests;

public class FallbackMiddlewareTests {
    private static async Task<(WebApplication app, HttpClient client)> StartAsync() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEggIdentityFallback(new FallbackBranding("TestApp", new Dictionary<string, string> {
            ["--color-bg"] = "#0b0d12",
        }));
        var app = builder.Build();

        app.Use(async (ctx, next) => {
            var roleHeader = ctx.Request.Headers["X-Test-Role"].ToString();
            if (roleHeader != "")
                ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(SessionClaims.Role, roleHeader)]));
            await next();
        });

        app.UseEggIdentityFallback();

        app.MapGet("/throws", () => { throw new InvalidOperationException("boom"); });
        app.MapGet("/ok", () => "ok");

        await app.StartAsync();
        return (app, app.GetTestClient());
    }

    [Fact]
    public async Task Throws_AnonymousUser_GetsGenericPage() {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var resp = await client.GetAsync("/throws");

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("boom", body);
    }

    [Fact]
    public async Task Throws_AdminUser_GetsStackTrace() {
        var (app, client) = await StartAsync();
        await using var _ = app;
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");

        var resp = await client.GetAsync("/throws");

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("boom", body);
    }

    [Fact]
    public async Task MaintenanceOn_BlocksNonAdmin() {
        var (app, client) = await StartAsync();
        await using var _ = app;
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
        await client.PostAsync("/admin/maintenance/on", null);
        client.DefaultRequestHeaders.Remove("X-Test-Role");

        var resp = await client.GetAsync("/ok");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
    }

    [Fact]
    public async Task MaintenanceOn_AdminBypasses() {
        var (app, client) = await StartAsync();
        await using var _ = app;
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
        await client.PostAsync("/admin/maintenance/on", null);

        var resp = await client.GetAsync("/ok");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task MaintenanceToggle_RejectsNonAdmin() {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var resp = await client.PostAsync("/admin/maintenance/on", null);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task UnmatchedRoute_GetsNotFoundPage() {
        var (app, client) = await StartAsync();
        await using var _ = app;

        var resp = await client.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("TestApp", body);
    }
}
