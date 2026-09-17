using System.Net;
using System.Text.Json;

namespace EggIdentity.Agent.Tests;

internal static class PortainerFakes {
    public const string Webhook = "05de31a2-79fa-4644-9c12-faa67e5c49f0";

    public const string ArmedAutoUpdate =
        """{"Interval":"","Webhook":"05de31a2-79fa-4644-9c12-faa67e5c49f0","JobID":"","ForceUpdate":true,"ForcePullImage":false}""";

    public const string GitStack =
        """{"Id":56,"Name":"ei-servers","Type":2,"EndpointId":9,"Env":[{"name":"A","value":"1"},{"name":"B","value":""}],"AutoUpdate":"""
        + ArmedAutoUpdate
        + ""","Option":{"Prune":true},"GitConfig":{"URL":"https://gitea:3000/EIStacks/ei-servers.git","ReferenceName":"refs/heads/main","ConfigFilePath":"docker-compose"""
        + """.yml","Authentication":{"Username":"portainer","Password":""},"ConfigHash":"abc","TLSSkipVerify":false}}""";

    public static string WebEditorStack(string env = "[]") =>
        """{"Id":3,"Name":"db","Type":2,"EndpointId":9,"Env":""" + env + ""","AutoUpdate":null,"GitConfig":null}""";

    public static JsonElement Body(FakePortainerHandler handler) => JsonSerializer.Deserialize<JsonElement>(handler.WriteBody!);

    public static List<(string, string)> Env(JsonElement root) =>
        [.. root.GetProperty("Env").EnumerateArray().Select(e => (e.GetProperty("name").GetString()!, e.GetProperty("value").GetString()!))];
}

internal sealed class FakePortainerHandler : HttpMessageHandler {
    private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new(StringComparer.Ordinal);

    public List<string> Calls { get; } = [];
    public HttpRequestMessage? Write { get; private set; }
    public string? WriteBody { get; private set; }

    public FakePortainerHandler On(string method, string pathAndQuery, string body = "", HttpStatusCode status = HttpStatusCode.OK) {
        _responses[$"{method} {pathAndQuery}"] = (status, body);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var key = $"{request.Method} {request.RequestUri!.PathAndQuery}";
        Calls.Add(key);
        if (request.Method != HttpMethod.Get) {
            Write = request;
            WriteBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        }
        var (status, body) = _responses.TryGetValue(key, out var found) ? found : (HttpStatusCode.NotFound, $"no fake response for {key}");
        return new HttpResponseMessage(status) { Content = new StringContent(body) };
    }
}
