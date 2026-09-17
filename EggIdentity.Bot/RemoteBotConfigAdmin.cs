using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace EggIdentity.Bot;

public sealed record BotAdminTarget(string App, Uri BaseUrl, string Secret) {
    public string Path { get; init; } = "/admin/api/bot";
}

public sealed class RemoteBotConfigAdmin(HttpClient http, BotAdminTarget target) : IBotConfigAdmin {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<BotConfigView> GetAsync(CancellationToken ct = default) =>
        SendAsync<BotConfigView>(HttpMethod.Get, null, ct);

    public Task<SaveResult> SaveAsync(BotConfigInput input, CancellationToken ct = default) =>
        SendAsync<SaveResult>(HttpMethod.Put, JsonContent.Create(input, options: Json), ct);

    private async Task<T> SendAsync<T>(HttpMethod method, HttpContent? content, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(target);

        var url = $"{target.BaseUrl.AbsoluteUri.TrimEnd('/')}/{target.Path.Trim('/')}";
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {target.Secret}");

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        if (!response.IsSuccessStatusCode) throw Failure(method, response);

        return await response.Content.ReadFromJsonAsync<T>(Json, ct)
            ?? throw new HttpRequestException($"{target.App} returned an empty body for {method} {target.Path}");
    }

    private HttpRequestException Failure(HttpMethod method, HttpResponseMessage response) {
        var code = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var detail = response.StatusCode switch {
            HttpStatusCode.Unauthorized => " (the admin secret for this app is wrong or missing)",
            HttpStatusCode.ServiceUnavailable => " (the app's bot is not running)",
            _ => "",
        };
        return new HttpRequestException(
            $"{target.App} returned {code} for {method} {target.Path}{detail}", null, response.StatusCode);
    }
}
