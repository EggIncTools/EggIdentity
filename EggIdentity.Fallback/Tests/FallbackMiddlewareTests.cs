using System.Net;
using System.Security.Claims;
using EggIdentity.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Fallback.Tests;

public sealed class FallbackHostFixture : IAsyncLifetime {
    private WebApplication _app = null!;

    public async Task InitializeAsync() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddEggIdentityFallback(new FallbackBranding("TestApp", new Dictionary<string, string> {
            ["--color-bg"] = "#0b0d12",
        }));
        var app = _app = builder.Build();

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
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    public HttpClient FreshClient() {
        _app.Services.GetRequiredService<MaintenanceState>().Set(false);
        return _app.GetTestClient();
    }
}

public class FallbackMiddlewareTests(FallbackHostFixture host) : IClassFixture<FallbackHostFixture> {
    [Fact]
    public async Task Throws_AnonymousUser_GetsGenericPage() {
        using var client = host.FreshClient();

        var resp = await client.GetAsync("/throws");

        Assert.Equal(HttpStatusCode.InternalServerError, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("boom", body);
    }

    [Fact]
    public async Task Throws_AdminUser_GetsStackTrace() {
        using var client = host.FreshClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");

        var resp = await client.GetAsync("/throws");

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("boom", body);
    }

    [Fact]
    public async Task MaintenanceOn_BlocksNonAdmin() {
        using var client = host.FreshClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
        await client.PostAsync("/admin/maintenance/on", null);
        client.DefaultRequestHeaders.Remove("X-Test-Role");

        var resp = await client.GetAsync("/ok");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
    }

    [Fact]
    public async Task MaintenanceOn_AdminBypasses() {
        using var client = host.FreshClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
        await client.PostAsync("/admin/maintenance/on", null);

        var resp = await client.GetAsync("/ok");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task MaintenanceToggle_RejectsNonAdmin() {
        using var client = host.FreshClient();

        var resp = await client.PostAsync("/admin/maintenance/on", null);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task UnmatchedRoute_GetsNotFoundPage() {
        using var client = host.FreshClient();

        var resp = await client.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("TestApp", body);
    }

    [Fact]
    public async Task MaintenanceAdminPage_RejectsNonAdmin() {
        using var client = host.FreshClient();

        var resp = await client.GetAsync("/admin/maintenance");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task MaintenanceAdminPage_AdminSeesToggle() {
        using var client = host.FreshClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");

        var resp = await client.GetAsync("/admin/maintenance");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("/admin/maintenance/on", body);
    }

    [Fact]
    public async Task Throws_JsonAccept_GetsJsonBody() {
        using var client = host.FreshClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var resp = await client.GetAsync("/throws");

        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("boom", body);
    }

    [Fact]
    public async Task MaintenanceOn_JsonAccept_GetsJsonBody() {
        using var client = host.FreshClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
        await client.PostAsync("/admin/maintenance/on", null);
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var resp = await client.GetAsync("/ok");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnmatchedRoute_JsonAccept_GetsJsonBody() {
        using var client = host.FreshClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var resp = await client.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("application/json", resp.Content.Headers.ContentType?.MediaType);
    }
}
