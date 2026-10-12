using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EggIdentity.Contract;

namespace EggIdentity.Bot;

public sealed record DmFile(string FileName, byte[] Content, string? ContentType = null);

public sealed record DmMessage {
    public string? Content { get; init; }
    public IReadOnlyList<JsonObject>? Embeds { get; init; }
    public IReadOnlyList<DmFile>? Files { get; init; }
}

public interface IDiscordDmClient {
    Task<string?> OpenChannelAsync(string discordUserId, CancellationToken ct);
    Task<DmSendResult> SendAsync(string discordUserId, DmMessage message, CancellationToken ct);
    Task<bool?> IsGuildMemberAsync(string guildId, string discordUserId, CancellationToken ct);
}

public sealed class DiscordDmClient(IHttpClientFactory httpClientFactory, string botToken) : IDiscordDmClient {
    public const string ApiBase = "https://discord.com/api/v10";
    public const int CannotDmCode = 50007;
    public const int UnknownUserCode = 10013;

    public async Task<string?> OpenChannelAsync(string discordUserId, CancellationToken ct) =>
        (await OpenAsync(discordUserId, ct)).ChannelId;

    public async Task<DmSendResult> SendAsync(string discordUserId, DmMessage message, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(message);
        var (channelId, openError) = await OpenAsync(discordUserId, ct);
        if (channelId is null) return openError ?? new DmSendResult(DmSendStatus.Failed);
        using var req = Request(HttpMethod.Post, $"{ApiBase}/channels/{channelId}/messages");
        req.Content = BuildContent(message);
        using var resp = await Client().SendAsync(req, ct);
        if (resp.IsSuccessStatusCode) return new DmSendResult(DmSendStatus.Sent) { ChannelId = channelId };
        var body = await resp.Content.ReadAsStringAsync(ct);
        return MapError((int)resp.StatusCode, body, resp.Headers.RetryAfter?.Delta) with { ChannelId = channelId };
    }

    public async Task<bool?> IsGuildMemberAsync(string guildId, string discordUserId, CancellationToken ct) {
        using var req = Request(HttpMethod.Get, $"{ApiBase}/guilds/{guildId}/members/{discordUserId}");
        using var resp = await Client().SendAsync(req, ct);
        if (resp.IsSuccessStatusCode) return true;
        return resp.StatusCode == HttpStatusCode.NotFound ? false : null;
    }

    public static HttpContent BuildContent(DmMessage message) {
        ArgumentNullException.ThrowIfNull(message);
        var payload = new JsonObject();
        if (message.Content is { Length: > 0 } content) payload["content"] = content;
        if (message.Embeds is { Count: > 0 } embeds)
            payload["embeds"] = new JsonArray([.. embeds.Select(e => e.DeepClone())]);
        if (message.Files is not { Count: > 0 } files)
            return new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        var attachments = new JsonArray();
        for (var i = 0; i < files.Count; i++)
            attachments.Add(new JsonObject { ["id"] = i, ["filename"] = files[i].FileName });
        payload["attachments"] = attachments;

        var multipart = new MultipartFormDataContent();
        var json = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        multipart.Add(json, "payload_json");
        for (var i = 0; i < files.Count; i++) {
            var part = new ByteArrayContent(files[i].Content);
            part.Headers.ContentType = new MediaTypeHeaderValue(files[i].ContentType ?? "application/octet-stream");
            multipart.Add(part, $"files[{i}]", files[i].FileName);
        }

        return multipart;
    }

    public static DmSendResult MapError(int httpStatus, string? body, TimeSpan? retryAfterHeader) {
        var (code, retryAfter) = ParseBody(body);
        var status = httpStatus == 429 ? DmSendStatus.RateLimited : code switch {
            CannotDmCode => DmSendStatus.CannotDm,
            UnknownUserCode => DmSendStatus.UnknownUser,
            _ => DmSendStatus.Failed,
        };
        return new DmSendResult(status) {
            RetryAfter = status == DmSendStatus.RateLimited ? retryAfter ?? retryAfterHeader : null,
            HttpStatus = httpStatus,
            DiscordCode = code,
        };
    }

    private async Task<(string? ChannelId, DmSendResult? Error)> OpenAsync(string discordUserId, CancellationToken ct) {
        using var req = Request(HttpMethod.Post, $"{ApiBase}/users/@me/channels");
        req.Content = new StringContent(new JsonObject { ["recipient_id"] = discordUserId }.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await Client().SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (resp.IsSuccessStatusCode)
            return ParseChannelId(body) is { } id ? (id, null) : (null, new DmSendResult(DmSendStatus.Failed) { HttpStatus = (int)resp.StatusCode });
        var error = MapError((int)resp.StatusCode, body, resp.Headers.RetryAfter?.Delta);
        if (resp.StatusCode == HttpStatusCode.NotFound && error.Status == DmSendStatus.Failed)
            error = error with { Status = DmSendStatus.UnknownUser };
        return (null, error);
    }

    private HttpClient Client() {
        var http = httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(10);
        return http;
    }

    private HttpRequestMessage Request(HttpMethod method, string url) {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken);
        return req;
    }

    private static string? ParseChannelId(string body) {
        try {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.String
                    ? id.GetString()
                    : null;
        } catch (JsonException) {
            return null;
        }
    }

    private static (int? Code, TimeSpan? RetryAfter) ParseBody(string? body) {
        if (string.IsNullOrWhiteSpace(body)) return (null, null);
        try {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return (null, null);
            int? code = doc.RootElement.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var n) ? n : null;
            TimeSpan? retry = doc.RootElement.TryGetProperty("retry_after", out var r) ? ParseSeconds(r) : null;
            return (code, retry);
        } catch (JsonException) {
            return (null, null);
        }
    }

    private static TimeSpan? ParseSeconds(JsonElement value) =>
        value.ValueKind switch {
            JsonValueKind.Number when value.TryGetDouble(out var s) && s >= 0 => TimeSpan.FromSeconds(s),
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s >= 0 => TimeSpan.FromSeconds(s),
            _ => null,
        };
}
