using System.Net;
using System.Text;

namespace EggIdentity.Settings.Api.Tests;

public class AdminApiClientTests {
    private static readonly AdminTarget Target =
        new("eggledger", new Uri("http://eggledger.test"), "s3cret");

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
            Requests.Add(request);
            return respond(request);
        }

        public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private static (AdminApiClient Client, FakeHandler Handler) Make(
        Func<HttpRequestMessage, HttpResponseMessage> respond) {
        var handler = new FakeHandler(respond);
        return (new AdminApiClient(new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task EveryRequest_CarriesTheAppSecretAsBearer() {
        var (client, handler) = Make(_ => FakeHandler.Json("""{"app":"eggledger","settings":[]}"""));

        await client.GetSettingsAsync(Target);

        Assert.Equal("Bearer s3cret", handler.Requests[0].Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task PathsAreBuiltUnderTheTargetPrefix() {
        var (client, handler) = Make(_ => FakeHandler.Json("""{"ok":true}"""));

        await client.SaveAsync(Target, "identity.api_port", "8090", "david");

        Assert.Equal(
            "http://eggledger.test/admin/api/settings/identity.api_port",
            handler.Requests[0].RequestUri?.AbsoluteUri);
        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
    }

    [Fact]
    public async Task KeysAndIdsAreEscaped() {
        var (client, handler) = Make(_ => FakeHandler.Json("""{"ok":true}"""));

        await client.DeleteRowAsync(Target, "deploy.apps", "app/with space");

        Assert.Equal(
            "http://eggledger.test/admin/api/collections/deploy.apps/app%2Fwith%20space",
            handler.Requests[0].RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task SaveSendsTheValueAndTheActor() {
        var (client, handler) = Make(_ => FakeHandler.Json("""{"ok":true,"restartRequired":true}"""));

        var result = await client.SaveAsync(Target, "a.one", "x", "david");

        Assert.Contains("\"value\":\"x\"", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"updatedBy\":\"david\"", handler.Bodies[0], StringComparison.Ordinal);
        Assert.True(result.Ok);
        Assert.True(result.RestartRequired);
    }

    [Fact]
    public async Task AnUnauthorizedResponse_SaysTheSecretIsWrong() {
        var (client, _) = Make(_ => FakeHandler.Json("", HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetSettingsAsync(Target));

        Assert.Contains("eggledger", ex.Message, StringComparison.Ordinal);
        Assert.Contains("admin secret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableApp_YieldsAnUnavailableDriftReport_NotAnException() {
        var (client, _) = Make(_ => throw new HttpRequestException("connection refused"));

        var drift = await client.GetDriftAsync(Target);

        Assert.False(drift.Available);
        Assert.Equal("eggledger", drift.App);
        Assert.Contains("connection refused", drift.Unavailable, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DriftFromAReachableApp_IsReturnedAsSent() {
        var (client, _) = Make(_ => FakeHandler.Json(
            """{"app":"eggledger","available":true,"problemCount":2,"entries":[{"key":"STRAY","reason":"Undeclared"}]}"""));

        var drift = await client.GetDriftAsync(Target);

        Assert.True(drift.Available);
        Assert.Equal(2, drift.ProblemCount);
        Assert.Equal("STRAY", drift.Entries[0].Key);
    }

    [Fact]
    public async Task ACustomPrefix_IsHonoured() {
        var (client, handler) = Make(_ => FakeHandler.Json("""{"app":"x","settings":[]}"""));

        await client.GetSettingsAsync(Target with { Prefix = "/internal/admin" });

        Assert.Equal("http://eggledger.test/internal/admin/settings", handler.Requests[0].RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task ABaseUrlWithAPath_IsPreserved() {
        var (client, handler) = Make(_ => FakeHandler.Json("""{"app":"x","settings":[]}"""));

        await client.GetSettingsAsync(Target with { BaseUrl = new Uri("http://host.test/eggledger/") });

        Assert.Equal("http://host.test/eggledger/admin/api/settings", handler.Requests[0].RequestUri?.AbsoluteUri);
    }
}
