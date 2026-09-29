using System.Net;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EggIdentity.Settings.Api.Tests;

public sealed class ManifestHosts : IAsyncLifetime {
    public const string Secret = "correct-horse-battery-staple";

    private sealed class NoopRestart : IRestartTrigger {
        public Task<string?> RestartAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }

    public IHost Bare { get; private set; } = null!;

    public IHost WithCloneAndRestart { get; private set; } = null!;

    public async Task InitializeAsync() {
        Bare = await StartAsync(extended: false);
        WithCloneAndRestart = await StartAsync(extended: true);
    }

    public async Task DisposeAsync() {
        foreach (var host in new[] { Bare, WithCloneAndRestart }) {
            await host.StopAsync();
            host.Dispose();
        }
    }

    private static async Task<IHost> StartAsync(bool extended) =>
        await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => {
                    services.AddRouting();
                    if (extended) services.AddSingleton<IRestartTrigger, NoopRestart>();
                })
                .Configure(app => {
                    app.UseRouting();
                    app.UseEndpoints(routes => {
                        var group = routes.MapAdminApi(new AdminApiOptions("testapp", Secret) { Version = "1.2.3", Revision = "abc" });
                        if (extended) group.MapGet("/clone", () => Results.Ok()).WithMetadata(new AdminCapability(AdminCapabilities.Clone));
                        routes.MapGet("/elsewhere", () => Results.Ok()).WithMetadata(new AdminCapability("stray"));
                    });
                }))
            .StartAsync();
}

public class ManifestEndpointTests(ManifestHosts hosts) : IClassFixture<ManifestHosts> {
    private static async Task<(HttpStatusCode Status, AdminManifest? Manifest)> GetAsync(IHost host, string? secret = ManifestHosts.Secret) {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/api/manifest");
        if (secret is not null) request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {secret}");
        var response = await host.GetTestClient().SendAsync(request);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AdminManifest>() : null);
    }

    [Fact]
    public async Task BareAdminApi_ListsItsOwnCapabilitiesAndIdentity() {
        var (status, manifest) = await GetAsync(hosts.Bare);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(manifest);
        Assert.Equal("testapp", manifest.App);
        Assert.Equal("1.2.3", manifest.Version);
        Assert.Equal("abc", manifest.Revision);
        Assert.Equal([AdminCapabilities.Collections, AdminCapabilities.Drift, AdminCapabilities.Settings], manifest.Capabilities);
    }

    [Fact]
    public async Task RoutesTaggedOnTheGroup_AppearAndRoutesElsewhereDoNot() {
        var (_, manifest) = await GetAsync(hosts.WithCloneAndRestart);

        Assert.True(manifest?.Has(AdminCapabilities.Clone));
        Assert.False(manifest?.Has("stray"));
    }

    [Fact]
    public async Task RestartAppears_OnlyWithATrigger() {
        Assert.False((await GetAsync(hosts.Bare)).Manifest?.Has(AdminCapabilities.Restart));
        Assert.True((await GetAsync(hosts.WithCloneAndRestart)).Manifest?.Has(AdminCapabilities.Restart));
    }

    [Fact]
    public async Task Manifest_IsBehindTheBearerGate() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(hosts.Bare, secret: null)).Status);
}
