using System.Net;

namespace EggIdentity.Agent;

public sealed class StackBusyException(string message) : Exception(message);

public interface IStackWebhook {
    Task InvokeAsync(Uri url, CancellationToken ct);
}

public sealed class StackWebhookClient(HttpClient http) : IStackWebhook {
    private const int BodyLimit = 300;

    public async Task InvokeAsync(Uri url, CancellationToken ct) {
        using var response = await http.PostAsync(url, null, ct);
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Conflict) throw new StackBusyException("portainer is already redeploying this stack");
        var body = (await response.Content.ReadAsStringAsync(ct)).Trim();
        var detail = body.Length == 0 ? "" : ": " + (body.Length > BodyLimit ? body[..BodyLimit] : body);
        throw new InvalidOperationException($"portainer webhook returned {(int)response.StatusCode}{detail}");
    }
}
