using System.Net;
using EggIdentity.Auth;
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
}
