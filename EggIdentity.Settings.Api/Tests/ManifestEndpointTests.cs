using System.Net;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EggIdentity.Settings.Api.Tests;

public class ManifestEndpointTests {
    private const string Secret = "correct-horse-battery-staple";

    private sealed class NoopRestart : IRestartTrigger {
        public Task<string?> RestartAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }

    private static async Task<IHost> StartAsync(bool clone = false, bool restart = false) =>
        await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => {
                    services.AddRouting();
                    if (restart) services.AddSingleton<IRestartTrigger, NoopRestart>();
                })
                .Configure(app => {
                    app.UseRouting();
                    app.UseEndpoints(routes => {
                        var group = routes.MapAdminApi(new AdminApiOptions("testapp", Secret) { Version = "1.2.3", Revision = "abc" });
                        if (clone) group.MapGet("/clone", () => Results.Ok()).WithMetadata(new AdminCapability(AdminCapabilities.Clone));
                        routes.MapGet("/elsewhere", () => Results.Ok()).WithMetadata(new AdminCapability("stray"));
                    });
                }))
            .StartAsync();

    private static async Task<(HttpStatusCode Status, AdminManifest? Manifest)> GetAsync(IHost host, string? secret = Secret) {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/api/manifest");
        if (secret is not null) request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {secret}");
        var response = await host.GetTestClient().SendAsync(request);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminManifest>() : null);
    }

    [Fact]
    public async Task BareAdminApi_ListsItsOwnCapabilitiesAndIdentity() {
        using var host = await StartAsync();

        var (status, manifest) = await GetAsync(host);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(manifest);
        Assert.Equal("testapp", manifest.App);
        Assert.Equal("1.2.3", manifest.Version);
        Assert.Equal("abc", manifest.Revision);
        Assert.Equal([AdminCapabilities.Collections, AdminCapabilities.Drift, AdminCapabilities.Settings], manifest.Capabilities);
    }

    [Fact]
    public async Task RoutesTaggedOnTheGroup_AppearAndRoutesElsewhereDoNot() {
        using var host = await StartAsync(clone: true);

        var (_, manifest) = await GetAsync(host);

        Assert.True(manifest?.Has(AdminCapabilities.Clone));
        Assert.False(manifest?.Has("stray"));
    }

    [Fact]
    public async Task RestartAppears_OnlyWithATrigger() {
        using var without = await StartAsync();
        using var with = await StartAsync(restart: true);

        Assert.False((await GetAsync(without)).Manifest?.Has(AdminCapabilities.Restart));
        Assert.True((await GetAsync(with)).Manifest?.Has(AdminCapabilities.Restart));
    }

    [Fact]
    public async Task Manifest_IsBehindTheBearerGate() {
        using var host = await StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(host, secret: null)).Status);
    }
}
