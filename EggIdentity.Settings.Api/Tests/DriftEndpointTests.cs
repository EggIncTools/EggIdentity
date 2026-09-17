using System.Net;
using System.Net.Http.Json;
using EggIdentity.Contract;
using EggIdentity.Settings.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace EggIdentity.Settings.Api.Tests;

public class DriftEndpointTests {
    private const string Secret = "correct-horse-battery-staple";
    private const string StrayKey = "EGGIDENTITY_DRIFT_ENDPOINT_STRAY";
    private const string DeclaredKey = "EGGIDENTITY_DRIFT_ENDPOINT_DECLARED";

    private sealed class Provider : ISettingsProvider {
        public IReadOnlyList<SettingDescriptor> Describe() => [
            new("drift.declared", DeclaredKey, "Declared", "Test",
                SettingKind.Text, ApplyTier.Bootstrap, Sensitivity.Plain),
            new("drift.missing", "EGGIDENTITY_DRIFT_ENDPOINT_MISSING", "Missing", "Test",
                SettingKind.Text, ApplyTier.Bootstrap, Sensitivity.Plain) { Required = true },
        ];
    }

    private sealed class FixedEnv(params EnvKeyInfo[] keys) : IEnvSource {
        public Task<IReadOnlyList<EnvKeyInfo>> GetAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EnvKeyInfo>>(keys);
    }

    private static async Task<IHost> StartAsync(string conn, IEnvSource? env) {
        var registry = new SettingsRegistry([new Provider()]);
        var source = new NpgsqlDataSourceBuilder(conn).Build();
        var store = new SettingsStore(source);
        await store.MigrateAsync(CancellationToken.None);

        return await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => {
                    services.AddRouting();
                    services.AddSingleton(registry);
                    services.AddSingleton(source);
                    services.AddSingleton(store);
                    services.AddSingleton(sp => new SettingsCache(registry, store));
                    services.AddSingleton<SettingsAdminService>();
                    if (env is not null) services.AddSingleton(env);
                })
                .Configure(app => {
                    app.UseRouting();
                    app.UseEndpoints(routes => routes.MapAdminApi(new AdminApiOptions("testapp", Secret)));
                }))
            .StartAsync();
    }

    private static async Task<AdminDriftResponse> DriftAsync(IHost host) {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/api/drift");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Secret}");
        var response = await host.GetTestClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminDriftResponse>())!;
    }

    [Fact]
    public async Task WithNoEnvSourceRegistered_DriftIsStillAvailable() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrWhiteSpace(conn)) return;

        Environment.SetEnvironmentVariable(StrayKey, "value");
        try {
            using var host = await StartAsync(conn, null);

            var drift = await DriftAsync(host);

            Assert.True(drift.Available);
            Assert.Null(drift.Unavailable);
            Assert.Contains(drift.Entries, e => e.Key == StrayKey && e.Reason == nameof(DriftReason.Undeclared));
            Assert.Contains(
                drift.Entries,
                e => e.Key == "EGGIDENTITY_DRIFT_ENDPOINT_MISSING" && e.Reason == nameof(DriftReason.MissingRequired));
        } finally {
            Environment.SetEnvironmentVariable(StrayKey, null);
        }
    }

    [Fact]
    public async Task WithNoEnvSourceRegistered_ADeclaredKeyThatIsSet_IsMatchedNotMissing() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrWhiteSpace(conn)) return;

        Environment.SetEnvironmentVariable(DeclaredKey, "value");
        try {
            using var host = await StartAsync(conn, null);

            var drift = await DriftAsync(host);

            Assert.Contains(drift.Entries, e => e.Key == DeclaredKey && e.Reason == nameof(DriftReason.Matched));
        } finally {
            Environment.SetEnvironmentVariable(DeclaredKey, null);
        }
    }

    [Fact]
    public async Task ARegisteredEnvSourceWins_SoTheAgentViewIsNotReplacedByTheFallback() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrWhiteSpace(conn)) return;

        Environment.SetEnvironmentVariable(StrayKey, "value");
        try {
            var env = new FixedEnv(new EnvKeyInfo("FROM_THE_AGENT", EnvOrigin.StackVariable) { Referenced = false });
            using var host = await StartAsync(conn, env);

            var drift = await DriftAsync(host);

            Assert.True(drift.Available);
            Assert.Contains(drift.Entries, e => e.Key == "FROM_THE_AGENT");
            Assert.DoesNotContain(drift.Entries, e => e.Key == StrayKey);
        } finally {
            Environment.SetEnvironmentVariable(StrayKey, null);
        }
    }

    [Fact]
    public async Task DriftStaysBehindTheBearerGate() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrWhiteSpace(conn)) return;

        using var host = await StartAsync(conn, null);

        var response = await host.GetTestClient().GetAsync(new Uri("/admin/api/drift", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
