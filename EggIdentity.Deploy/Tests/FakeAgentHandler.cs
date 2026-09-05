using System.IO.Pipelines;
using System.Net;
using System.Text;
using EggIdentity.Auth;
using EggIdentity.Contract;

namespace EggIdentity.Deploy.Tests;

public sealed class FakeAgentHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler {
    private int _calls;

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string?> Bodies { get; } = [];

    public int Calls => _calls;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var call = Interlocked.Increment(ref _calls);
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests) {
            Requests.Add(request);
            Bodies.Add(body);
        }
        return respond(request, call);
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Text(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/plain") };

    public static HttpResponseMessage Stream(Pipe pipe) =>
        new(HttpStatusCode.OK) { Content = new StreamContent(pipe.Reader.AsStream()) };

    public static string Frame(DeployEvent evt) =>
        $"id: {evt.Id}\nevent: deploy\ndata: {System.Text.Json.JsonSerializer.Serialize(evt)}\n\n";

    public static async Task WriteAsync(Pipe pipe, string text) {
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(text));
        await pipe.Writer.FlushAsync();
    }
}

public static class TestFixtures {
    public static readonly SessionCookieOptions Session = new() {
        SigningSecret = "0123456789abcdef0123456789abcdef0123456789abcdef",
        CookieName = "egg_test_session",
    };

    public static DeployOptions Options(string app = "eggledger") => new("http://agent.test", app) {
        ReconnectDelay = TimeSpan.Zero,
        MaxReconnectDelay = TimeSpan.Zero,
        CallTimeout = TimeSpan.FromSeconds(5),
    };

    public static AgentClient Client(FakeAgentHandler handler, DeployOptions? options = null) {
        var opts = options ?? Options();
        var http = new HttpClient(handler) { BaseAddress = opts.BaseAddress, Timeout = Timeout.InfiniteTimeSpan };
        return new AgentClient(http, opts, Session);
    }

    public static DeployEvent Event(long id, string app = "eggledger", DeployPhase phase = DeployPhase.Checked, string message = "checked") =>
        new(id, app, phase, message, new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero), null, null, null, null);
}
