using System.Net;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace EggIdentity.Fleet.Tests;

public class FleetRoutesTests {
    private const string HookSecret = "hook-secret";
    private const string Ledger = "eggledger";

    private static async Task<WebApplication> StartAsync() {
        IReadOnlyList<SuiteApp> rows = [new SuiteApp { Name = Ledger, Stack = "egg-apps" }];
        var service = new DeployService(_ => Task.FromResult(rows), null, new NoRegistry(), new DeployEventRing(firstId: 1));
        var runtime = new FleetRuntime(service, () => HookSecret);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.MapGroup("/admin/api").MapFleetAdminApi(runtime);
        app.MapFleetHook(runtime);
        await app.StartAsync();
        return app;
    }

    private static HttpRequestMessage Hook(string app, string? secret) {
        var request = new HttpRequestMessage(HttpMethod.Post, FleetRoutes.HookPath) {
            Content = JsonContent.Create(new DeployHookPayload(app, "sha256:abc", "rev", "v1")),
        };
        if (secret is not null) request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {secret}");
        return request;
    }

    [Fact]
    public async Task Hook_WrongSecret_IsUnauthorized() {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().SendAsync(Hook(Ledger, "nope"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hook_UnknownApp_IsNotFound() {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().SendAsync(Hook("ghost", HookSecret));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Hook_KnownApp_IsAcceptedAndRecordsRelease() {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().SendAsync(Hook(Ledger, HookSecret));
        var status = await response.Content.ReadFromJsonAsync<DeployStatus>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("sha256:abc", status?.LatestDigest);
    }

    [Fact]
    public async Task Status_ListsEnabledApps() {
        await using var app = await StartAsync();

        var statuses = await app.GetTestClient().GetFromJsonAsync<List<DeployStatus>>("/admin/api/fleet/status");

        Assert.Equal([Ledger], statuses?.Select(s => s.App));
    }

    [Fact]
    public async Task Restart_WithoutPortainer_ReportsRefusal() {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().PostAsync($"/admin/api/fleet/restart/{Ledger}", null);
        var body = await response.Content.ReadFromJsonAsync<AdminSaveResponse>();

        Assert.NotNull(body);
        Assert.False(body.Ok);
        Assert.Contains("portainer.api_url", body.Error, StringComparison.Ordinal);
    }

    private sealed class NoRegistry : IImageRegistry {
        public Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) => throw new InvalidOperationException("unused");
    }
}
