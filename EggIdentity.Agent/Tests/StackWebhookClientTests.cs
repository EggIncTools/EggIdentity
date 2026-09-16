using System.Net;

namespace EggIdentity.Agent.Tests;

public class StackWebhookClientTests {
    private static readonly Uri Url = new("https://portainer.test/api/stacks/webhooks/secret-token");

    private sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static (StackWebhookClient Client, FakeHandler Handler) Build(HttpStatusCode status, string body = "") {
        var handler = new FakeHandler(status, body);
        return (new StackWebhookClient(new HttpClient(handler)), handler);
    }

    [Fact]
    public async Task Invoke_Accepted_PostsWithEmptyBody() {
        var (client, handler) = Build(HttpStatusCode.Accepted);

        await client.InvokeAsync(Url, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(Url, handler.Request.RequestUri);
        Assert.True(string.IsNullOrEmpty(handler.RequestBody));
    }

    [Fact]
    public async Task Invoke_Conflict_ThrowsStackBusy() {
        var (client, _) = Build(HttpStatusCode.Conflict);

        await Assert.ThrowsAsync<StackBusyException>(() => client.InvokeAsync(Url, CancellationToken.None));
    }

    [Fact]
    public async Task Invoke_ServerError_ThrowsWithStatusAndBodyButNotUrl() {
        var (client, _) = Build(HttpStatusCode.InternalServerError, "stack deploy failed");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.InvokeAsync(Url, CancellationToken.None));

        Assert.Contains("500", error.Message, StringComparison.Ordinal);
        Assert.Contains("stack deploy failed", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("portainer.test", error.Message, StringComparison.Ordinal);
    }
}
