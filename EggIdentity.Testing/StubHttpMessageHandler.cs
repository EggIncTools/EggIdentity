using System.Net;

namespace EggIdentity.Testing;

public sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
    private readonly List<HttpRequestMessage> _requests = [];

    public StubHttpMessageHandler(HttpStatusCode status, string body = "") : this(_ => new HttpResponseMessage(status) { Content = new StringContent(body) }) {
    }

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    public HttpRequestMessage? LastRequest { get; private set; }

    public string? LastRequestBody { get; private set; }

    public HttpClient Client(string baseAddress = "https://stub.test/") => new(this, false) { BaseAddress = new Uri(baseAddress) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        _requests.Add(request);
        LastRequest = request;
        LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return respond(request);
    }
}
