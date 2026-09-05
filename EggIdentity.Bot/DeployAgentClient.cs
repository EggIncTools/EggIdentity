using System.Net.Http.Headers;
using System.Text.Json;
using EggIdentity.Contract;

namespace EggIdentity.Bot;

public sealed class DeployAgentClient {
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private const string DecodeError = "could not decode deploy agent response";

    public static async Task<DeployResponse> CallAsync(string agentUrl, string secret, CancellationToken ct = default) {
        try {
            using var req = new HttpRequestMessage(HttpMethod.Post, agentUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            using var resp = await Http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return new DeployResponse { Tail = $"deploy agent returned {(int)resp.StatusCode} {resp.ReasonPhrase}" };
            return Parse(await resp.Content.ReadAsStringAsync(ct));
        } catch (Exception ex) {
            return new DeployResponse { Tail = ex.Message };
        }
    }

    public static DeployResponse Parse(string json) {
        try {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new DeployResponse { Tail = DecodeError };
            if (doc.RootElement.TryGetProperty("app", out _)) {
                var status = doc.RootElement.Deserialize<DeployStatus>();
                return status is null ? new DeployResponse { Tail = DecodeError } : FromStatus(status);
            }
            return doc.RootElement.Deserialize<DeployResponse>() ?? new DeployResponse { Tail = DecodeError };
        } catch (JsonException) {
            return new DeployResponse { Tail = DecodeError };
        }
    }

    public static DeployResponse FromStatus(DeployStatus status) {
        ArgumentNullException.ThrowIfNull(status);
        var failed = status.LastEvent?.Phase == DeployPhase.Failed;
        return new DeployResponse {
            Ok = !failed,
            AlreadyUpToDate = !failed && !status.UpdateAvailable && !status.Busy,
            FromHash = Short(status.RunningRevision),
            ToHash = Short(status.LatestRevision ?? status.RunningRevision),
            Tail = status.LastEvent?.Message,
        };
    }

    private static string? Short(string? revision) =>
        revision is { Length: > 7 } ? revision[..7] : revision;
}
