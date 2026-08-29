using System.Text;
using System.Text.Json;
using EggIdentity.Contract;

namespace EggIdentity.Agent;

public sealed class Watcher(string name, string notifyBotUrl, string notifySecret) {
    private string _lastFail = "";

    internal Func<DeployResponse, int> Send = null!;

    internal void HandleResult(DeployResponse res) {
        Console.WriteLine($"watcher: {name}: tick: result ok={res.Ok} alreadyUpToDate={res.AlreadyUpToDate} from={res.FromHash} to={res.ToHash}");
        var toSend = Decide(res);
        if (toSend is null) {
            Console.WriteLine($"watcher: {name}: tick: no notification needed");
            return;
        }
        if (string.IsNullOrEmpty(notifyBotUrl)) {
            Console.WriteLine($"watcher: {name}: tick: notification needed but notify disabled (no notify_bot_url), staying silent");
            return;
        }
        try {
            var status = (Send ?? PostToBot)(toSend);
            Console.WriteLine($"watcher: {name}: tick: notified bot -> {status}");
        } catch (Exception e) { Console.Error.WriteLine($"watcher: {name}: notify: {e.Message}"); }
    }

    private int PostToBot(DeployResponse res) {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var json = JsonSerializer.Serialize(res);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var req = new HttpRequestMessage(HttpMethod.Post, notifyBotUrl) { Content = content };
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {notifySecret}");
        var resp = http.Send(req);
        return (int)resp.StatusCode;
    }

    internal DeployResponse? Decide(DeployResponse res) {
        if (res.Ok && res.AlreadyUpToDate) { _lastFail = ""; return null; }
        if (res.Ok) { _lastFail = ""; return res; }
        if (IsTransient(res.Tail)) return null;
        if (res.Tail == _lastFail) return null;
        _lastFail = res.Tail ?? "";
        return res;
    }

    private static readonly string[] TransientMarkers = [
        "Cannot connect to the Docker daemon", // host stopped Docker
        "context deadline exceeded", // GHCR/registry pull timed out
        "Client.Timeout exceeded", // Go http client timeout wrapper
        "TLS handshake timeout",
        "connection refused",
        "no such host", // DNS hiccup resolving the registry
        "i/o timeout",
        "temporary failure in name resolution",
    ];

    internal static bool IsTransient(string? tail) =>
        tail is not null && TransientMarkers.Any(m => tail.Contains(m, StringComparison.OrdinalIgnoreCase));
}
