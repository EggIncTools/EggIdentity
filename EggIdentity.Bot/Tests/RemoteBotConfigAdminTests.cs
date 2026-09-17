using System.Net;
using System.Text;

namespace EggIdentity.Bot.Tests;

public class RemoteBotConfigAdminTests {
    private static readonly BotAdminTarget Target =
        new("eggledger", new Uri("http://eggledger.test"), "s3cret");

    private const string ViewJson =
        """{"dashboardChannelId":"111","githubFeedThreadId":null,"deployNotificationsThreadId":null,"githubWebhookUrl":null,"successEmbedJson":null,"failureEmbedJson":null,"uptodateEmbedJson":null,"defaultSuccess":{"title":"deploy ok"},"defaultFailure":{"title":"deploy failed"},"defaultUptodate":{"title":"up to date"},"variables":[{"name":"app","desc":"app name"}],"dashboardEmbedJson":null,"defaultDashboard":{"title":"dashboard"},"dashboardVariables":[],"successMessageJson":null,"failureMessageJson":null,"uptodateMessageJson":null,"defaultSuccessMessage":{"kind":"embed","embed":{"title":"deploy ok"}},"defaultFailureMessage":{"kind":"embed","embed":{"title":"deploy failed"}},"defaultUptodateMessage":{"kind":"embed","embed":{"title":"up to date"}}}""";

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

    private static (RemoteBotConfigAdmin Admin, FakeHandler Handler) Make(
        Func<HttpRequestMessage, HttpResponseMessage> respond) {
        var handler = new FakeHandler(respond);
        return (new RemoteBotConfigAdmin(new HttpClient(handler), Target), handler);
    }

    [Fact]
    public async Task GetHitsTheBotPathWithTheBearer() {
        var (admin, handler) = Make(_ => FakeHandler.Json(ViewJson));

        var view = await admin.GetAsync();

        Assert.Equal("http://eggledger.test/admin/api/bot", handler.Requests[0].RequestUri?.AbsoluteUri);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("Bearer s3cret", handler.Requests[0].Headers.Authorization?.ToString());
        Assert.Equal("111", view.DashboardChannelId);
        Assert.Equal("deploy ok", view.DefaultSuccessMessage.Embed?.Title);
        Assert.Equal("app", view.Variables[0].Name);
    }

    [Fact]
    public async Task SavePutsTheInputAndReadsTheResult() {
        var (admin, handler) = Make(_ => FakeHandler.Json("""{"ok":true,"error":null,"githubWebhookUrl":"https://hook"}"""));

        var result = await admin.SaveAsync(new BotConfigInput("111", "222", "333", null, null, null));

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal("http://eggledger.test/admin/api/bot", handler.Requests[0].RequestUri?.AbsoluteUri);
        Assert.Contains("\"dashboardChannelId\":\"111\"", handler.Bodies[0], StringComparison.Ordinal);
        Assert.True(result.Ok);
        Assert.Equal("https://hook", result.GithubWebhookUrl);
    }

    [Fact]
    public async Task UnavailableBotSurfacesAs503() {
        var (admin, _) = Make(_ => FakeHandler.Json("""{"ok":false}""", HttpStatusCode.ServiceUnavailable));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => admin.GetAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }
}
