using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using EggIdentity.Auth;
using EggIdentity.Contract;
using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed class AgentClient(HttpClient http, DeployOptions options, SessionCookieOptions session) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly MediaTypeWithQualityHeaderValue EventStream = new("text/event-stream");

    public async Task<DeployStatus?> GetStatusAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Get, $"status/{Uri.EscapeDataString(app)}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return await ReadAsync<DeployStatus>(response, ct);
    }

    public async Task<IReadOnlyList<DeployStatus>> GetAllStatusAsync(CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Get, "status", null, ct);
        return await ReadAsync<List<DeployStatus>>(response, ct);
    }

    public async Task<DeployStatus> CheckAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Post, $"check/{Uri.EscapeDataString(app)}", null, ct);
        return await ReadAsync<DeployStatus>(response, ct);
    }

    public async Task<DeployStatus> DeployAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Post, $"deploy/{Uri.EscapeDataString(app)}", null, ct);
        return await ReadAsync<DeployStatus>(response, ct);
    }

    public async Task<string?> RestartAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Post, $"restart/{Uri.EscapeDataString(app)}", null, ct);
        return await FailureAsync(response, ct);
    }

    public async Task<string> GetLogsTailAsync(string app, int lines, CancellationToken ct) {
        var path = $"logs/{Uri.EscapeDataString(app)}/tail?lines={lines.ToString(CultureInfo.InvariantCulture)}";
        using var response = await SendAsync(HttpMethod.Get, path, null, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException(Describe(response, body), null, response.StatusCode);
        return body;
    }

    public async Task<IReadOnlyList<EnvKeyInfo>> GetEnvAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Get, $"env/{Uri.EscapeDataString(app)}", null, ct);
        var entries = await ReadAsync<List<EnvEntryWire>>(response, ct);
        return [.. entries.Where(e => !string.IsNullOrEmpty(e.Name)).Select(e => e.ToInfo())];
    }

    public async Task<string?> PatchStackEnvAsync(IReadOnlyDictionary<string, string?> changes, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(changes);
        using var response = await SendAsync(HttpMethod.Patch, "stack/env", JsonContent.Create(changes, options: Json), ct);
        return await FailureAsync(response, ct);
    }

    public async Task<string?> ReconcileStackAsync(CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Post, "stack/reconcile", null, ct);
        return await FailureAsync(response, ct);
    }

    public async IAsyncEnumerable<DeployEvent> StreamEventsAsync(long? afterId, [EnumeratorCancellation] CancellationToken ct) {
        using var request = NewRequest(HttpMethod.Get, "events");
        request.Headers.Accept.Add(EventStream);
        if (afterId is { } id) request.Headers.TryAddWithoutValidation("Last-Event-ID", id.ToString(CultureInfo.InvariantCulture));

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(Describe(response, body), null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        var parser = new SseParser();
        while (await ReadLineWithIdleTimeoutAsync(reader, ct) is { } line) {
            if (parser.Feed(line) is not { } message) continue;
            if (SseParser.TryReadDeployEvent(message, out var evt)) yield return evt;
        }
    }

    private async Task<string?> ReadLineWithIdleTimeoutAsync(StreamReader reader, CancellationToken ct) {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
        idle.CancelAfter(options.StreamIdleTimeout);
        try {
            return await reader.ReadLineAsync(idle.Token);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new TimeoutException($"agent event stream idle for {options.StreamIdleTimeout}");
        }
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path) {
        var request = new HttpRequestMessage(method, path);
        var caller = options.CallerName ?? options.AppName;
        var token = SessionToken.Issue(session, new SessionUser(caller, null, UserRoles.ToName(UserRole.Admin)), DateTimeOffset.UtcNow);
        request.Headers.TryAddWithoutValidation("Cookie", $"{session.CookieName}={token}");
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct) {
        using var request = NewRequest(method, path);
        request.Content = content;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(options.CallTimeout);
        try {
            return await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new TimeoutException($"agent {method} {path} timed out after {options.CallTimeout}");
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct) {
        if (!response.IsSuccessStatusCode) {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(Describe(response, body), null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct)
            ?? throw new HttpRequestException("agent returned an empty body", null, response.StatusCode);
    }

    private static async Task<string?> FailureAsync(HttpResponseMessage response, CancellationToken ct) {
        if (response.IsSuccessStatusCode) return null;
        return Describe(response, await response.Content.ReadAsStringAsync(ct));
    }

    private static string Describe(HttpResponseMessage response, string body) {
        var code = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var text = body.Trim();
        return text.Length == 0 ? $"agent returned {code}" : $"agent returned {code}: {text}";
    }

    private sealed class EnvEntryWire {
        public string? Name { get; set; }
        public JsonElement? Origin { get; set; }
        public bool? Masked { get; set; }
        public string? Value { get; set; }
        public bool? Referenced { get; set; }

        public EnvKeyInfo ToInfo() => new(Name!, ParseOrigin(Origin)) {
            Masked = Masked ?? false,
            Value = Value,
            Referenced = Referenced ?? true,
        };

        private static EnvOrigin ParseOrigin(JsonElement? element) {
            if (element is not { } value) return EnvOrigin.Runtime;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && Enum.IsDefined((EnvOrigin)number))
                return (EnvOrigin)number;
            if (value.ValueKind == JsonValueKind.String && Enum.TryParse<EnvOrigin>(value.GetString(), true, out var named))
                return named;
            return EnvOrigin.Runtime;
        }
    }
}
