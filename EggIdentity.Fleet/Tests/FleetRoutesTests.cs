using System.Net;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Fleet.Tests;

public sealed class FleetRoutesHost : IAsyncLifetime {
    public const string HookSecret = "hook-secret";
    public const string Ledger = "eggledger";

    private WebApplication _app = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync() {
        IReadOnlyList<SuiteApp> rows = [new SuiteApp { Name = Ledger, Stack = "egg-apps" }];
        var service = new DeployService(_ => Task.FromResult(rows), null, new NoRegistry(), new DeployEventRing(firstId: 1));
        var runtime = new FleetRuntime(service, () => HookSecret);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var app = _app = builder.Build();
        app.MapGroup("/admin/api").MapFleetAdminApi(runtime);
        app.MapFleetHook(runtime);
        await app.StartAsync();
        Client = app.GetTestClient();
    }

    public async Task DisposeAsync() {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    private sealed class NoRegistry : IImageRegistry {
        public Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) => throw new InvalidOperationException("unused");
    }
}

public class FleetRoutesTests(FleetRoutesHost host) : IClassFixture<FleetRoutesHost> {
    private const string HookSecret = FleetRoutesHost.HookSecret;
    private const string Ledger = FleetRoutesHost.Ledger;

    private static HttpRequestMessage Hook(string app, string? secret) {
        var request = new HttpRequestMessage(HttpMethod.Post, FleetRoutes.HookPath) {
            Content = JsonContent.Create(new DeployHookPayload(app, "sha256:abc", "rev", "v1")),
        };
        if (secret is not null) request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {secret}");
        return request;
    }

    [Fact]
    public async Task Hook_WrongSecret_IsUnauthorized() {
        var response = await host.Client.SendAsync(Hook(Ledger, "nope"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hook_UnknownApp_IsNotFound() {
        var response = await host.Client.SendAsync(Hook("ghost", HookSecret));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Hook_KnownApp_IsAcceptedAndRecordsRelease() {
        var response = await host.Client.SendAsync(Hook(Ledger, HookSecret));
        var status = await response.Content.ReadFromJsonAsync<DeployStatus>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("sha256:abc", status?.LatestDigest);
    }

    [Fact]
    public async Task Status_ListsEnabledApps() {
        var statuses = await host.Client.GetFromJsonAsync<List<DeployStatus>>("/admin/api/fleet/status");

        Assert.Equal([Ledger], statuses?.Select(s => s.App));
    }

    [Fact]
    public async Task Restart_WithoutPortainer_ReportsRefusal() {
        var response = await host.Client.PostAsync($"/admin/api/fleet/restart/{Ledger}", null);
        var body = await response.Content.ReadFromJsonAsync<AdminSaveResponse>();

        Assert.NotNull(body);
        Assert.False(body.Ok);
        Assert.Contains("portainer.api_url", body.Error, StringComparison.Ordinal);
    }
}
