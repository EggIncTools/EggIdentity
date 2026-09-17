using System.Net;
using EggIdentity.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EggIdentity.Settings.Api.Tests;

public class BearerGateTests {
    private const string Secret = "correct-horse-battery-staple";

    private static async Task<IHost> StartAsync() {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddRouting())
                .Configure(app => {
                    app.UseRouting();
                    app.UseEndpoints(routes => {
                        routes.MapAdminApi(new AdminApiOptions("testapp", Secret));
                        routes.MapGet("/admin/api/probe", () => Results.Ok("reached"));
                    });
                }))
            .StartAsync();
        return host;
    }

    private static async Task<HttpResponseMessage> GetAsync(IHost host, string path, string? authorization) {
        var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (authorization is not null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task NoAuthorizationHeader_IsRejected() {
        using var host = await StartAsync();

        var response = await GetAsync(host, "/admin/api/settings", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Bearer wrong-secret")]
    [InlineData("correct-horse-battery-staple")]
    [InlineData("Basic Y29ycmVjdA==")]
    [InlineData("Bearer ")]
    public async Task WrongCredential_IsRejected(string authorization) {
        using var host = await StartAsync();

        var response = await GetAsync(host, "/admin/api/settings", authorization);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EveryWriteRoute_IsGatedToo() {
        using var host = await StartAsync();
        var client = host.GetTestClient();

        var save = await client.PutAsJsonAsync("/admin/api/settings/a.one", new AdminSaveRequest { Value = "x" });
        var create = await client.PostAsJsonAsync("/admin/api/collections/deploy.apps", new AdminRowRequest { Id = "x" });
        var delete = await client.DeleteAsync("/admin/api/collections/deploy.apps/x");
        var restart = await client.PostAsync("/admin/api/restart", null);

        Assert.Equal(HttpStatusCode.Unauthorized, save.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, restart.StatusCode);
    }

    [Fact]
    public async Task TheGateCoversOnlyTheAdminApiGroup() {
        using var host = await StartAsync();

        var response = await GetAsync(host, "/admin/api/probe", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CorrectSecret_ReachesTheHandler() {
        using var host = await StartAsync();

        var response = await GetAsync(host, "/admin/api/drift", $"Bearer {Secret}");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void AnEmptySecret_IsRefusedAtWireUp() {
        var routes = WebApplication.CreateBuilder().Build();

        Assert.Throws<ArgumentException>(() => routes.MapAdminApi(new AdminApiOptions("testapp", "")));
    }
}
