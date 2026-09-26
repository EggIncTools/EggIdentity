using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using EggIdentity.Contract;
using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public sealed class FleetClient {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly MediaTypeWithQualityHeaderValue EventStream = new("text/event-stream");
    private readonly Func<HttpClient> _http;
    private readonly DeployOptions _options;

    public FleetClient(HttpClient http, DeployOptions options)
        : this(() => http, options) {
    }

    public FleetClient(IHttpClientFactory factory, DeployOptions options)
        : this(() => factory.CreateClient(DeployOptions.HttpClientName), options) {
    }

    private FleetClient(Func<HttpClient> http, DeployOptions options) {
        _http = http;
        _options = options;
    }

    public string AppName => _options.AppName;

    public async Task<DeployStatus?> GetStatusAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Get, $"status/{Uri.EscapeDataString(app)}", null, ct);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<DeployStatus>(response, ct);
    }

    public async Task<IReadOnlyList<DeployStatus>> GetAllStatusAsync(CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Get, "status", null, ct);
        return await ReadAsync<List<DeployStatus>>(response, ct);
    }

    public async Task<DeployStatus?> CheckAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Post, $"check/{Uri.EscapeDataString(app)}", null, ct);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<DeployStatus>(response, ct);
    }

    public async Task<DeployStatus?> DeployAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Post, $"deploy/{Uri.EscapeDataString(app)}", null, ct);
        return response.StatusCode == HttpStatusCode.NotFound ? null : await ReadAsync<DeployStatus>(response, ct);
    }

    public async Task<string?> RestartAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Post, $"restart/{Uri.EscapeDataString(app)}", null, ct);
        return await OutcomeAsync(response, ct);
    }

    public async Task<string> GetLogsTailAsync(string app, int lines, CancellationToken ct) {
        var path = $"logs/{Uri.EscapeDataString(app)}?lines={lines.ToString(CultureInfo.InvariantCulture)}";
        using var response = await SendAsync(HttpMethod.Get, path, null, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return response.IsSuccessStatusCode ? body : throw new HttpRequestException(Describe(response, body), null, response.StatusCode);
    }

    public async Task<IReadOnlyList<EnvKeyInfo>> GetEnvAsync(string app, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Get, $"env/{Uri.EscapeDataString(app)}", null, ct);
        var entries = await ReadAsync<List<EnvEntryWire>>(response, ct);
        return [.. entries.Where(e => !string.IsNullOrEmpty(e.Name)).Select(e => e.ToInfo())];
    }

    public async Task<string?> PatchStackEnvAsync(string app, IReadOnlyDictionary<string, string?> changes, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(changes);
        using var response = await SendAsync(HttpMethod.Patch, $"env/{Uri.EscapeDataString(app)}", JsonContent.Create(changes, options: Json), ct);
        return await OutcomeAsync(response, ct);
    }

    public async Task<IReadOnlyList<StackInfo>> GetStacksAsync(CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Get, "stacks", null, ct);
        return await ReadAsync<List<StackInfo>>(response, ct);
    }

    public async Task<string?> RedeployStackAsync(string stack, CancellationToken ct) {
        using var response = await SendAsync(HttpMethod.Post, $"stacks/{Uri.EscapeDataString(stack)}/redeploy", null, ct);
        return await OutcomeAsync(response, ct);
    }

    public async IAsyncEnumerable<DeployEvent> StreamEventsAsync(long? afterId, [EnumeratorCancellation] CancellationToken ct) {
        using var request = NewRequest(HttpMethod.Get, "events");
        request.Headers.Accept.Add(EventStream);
        if (afterId is { } id) request.Headers.TryAddWithoutValidation("Last-Event-ID", id.ToString(CultureInfo.InvariantCulture));

        using var response = await _http().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
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
        idle.CancelAfter(_options.StreamIdleTimeout);
        try {
            return await reader.ReadLineAsync(idle.Token);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new TimeoutException($"fleet event stream idle for {_options.StreamIdleTimeout}");
        }
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path) {
        var request = new HttpRequestMessage(method, new Uri(_options.BaseAddress, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Secret);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct) {
        using var request = NewRequest(method, path);
        request.Content = content;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.CallTimeout);
        try {
            return await _http().SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token);
        } catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            throw new TimeoutException($"fleet {method} {path} timed out after {_options.CallTimeout}");
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct) {
        if (!response.IsSuccessStatusCode) {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(Describe(response, body), null, response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<T>(Json, ct)
            ?? throw new HttpRequestException("fleet returned an empty body", null, response.StatusCode);
    }

    private static async Task<string?> OutcomeAsync(HttpResponseMessage response, CancellationToken ct) {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) return Describe(response, body);
        try {
            var outcome = JsonSerializer.Deserialize<AdminSaveResponse>(body, Json);
            return outcome is { Ok: false } ? outcome.Error ?? "the fleet refused the request" : null;
        } catch (JsonException) {
            return null;
        }
    }

    private static string Describe(HttpResponseMessage response, string body) {
        var code = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var text = body.Trim();
        return response.StatusCode == HttpStatusCode.Unauthorized
            ? $"fleet returned {code}: the identity API secret is wrong or missing"
            : text.Length == 0 ? $"fleet returned {code}" : $"fleet returned {code}: {text}";
    }

    private sealed class EnvEntryWire {
        public string? Name { get; set; }
        public JsonElement? Origin { get; set; }
        public bool? Masked { get; set; }
        public string? Value { get; set; }
        public bool? Referenced { get; set; }

        public EnvKeyInfo ToInfo() => new(Name ?? throw new InvalidOperationException("env entry has no name"), ParseOrigin(Origin)) {
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
