using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EggIdentity.Contract;

namespace EggIdentity.Settings.Api;

public sealed record AdminTarget(string App, Uri BaseUrl, string Secret) {
    public string Prefix { get; init; } = "/admin/api";
}

public sealed class AdminApiClient(HttpClient http) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<AdminSettingsResponse> GetSettingsAsync(AdminTarget target, CancellationToken ct = default) =>
        SendAsync<AdminSettingsResponse>(target, HttpMethod.Get, "settings", null, ct);

    public Task<AdminSaveResponse> SaveAsync(
        AdminTarget target, string key, string? value, string? updatedBy, CancellationToken ct = default) =>
        SendAsync<AdminSaveResponse>(
            target, HttpMethod.Put, $"settings/{Uri.EscapeDataString(key)}",
            JsonContent.Create(new AdminSaveRequest { Value = value, UpdatedBy = updatedBy }, options: Json), ct);

    public Task<IReadOnlyList<AdminCollectionWire>> GetCollectionsAsync(AdminTarget target, CancellationToken ct = default) =>
        SendAsync<IReadOnlyList<AdminCollectionWire>>(target, HttpMethod.Get, "collections", null, ct);

    public Task<AdminCollectionResponse> GetCollectionAsync(AdminTarget target, string key, CancellationToken ct = default) =>
        SendAsync<AdminCollectionResponse>(target, HttpMethod.Get, $"collections/{Uri.EscapeDataString(key)}", null, ct);

    public Task<AdminSaveResponse> CreateRowAsync(
        AdminTarget target, string key, AdminRowRequest row, CancellationToken ct = default) =>
        SendAsync<AdminSaveResponse>(
            target, HttpMethod.Post, $"collections/{Uri.EscapeDataString(key)}",
            JsonContent.Create(row, options: Json), ct);

    public Task<AdminSaveResponse> SaveRowAsync(
        AdminTarget target, string key, string id, AdminRowRequest row, CancellationToken ct = default) =>
        SendAsync<AdminSaveResponse>(
            target, HttpMethod.Put, $"collections/{Uri.EscapeDataString(key)}/{Uri.EscapeDataString(id)}",
            JsonContent.Create(row, options: Json), ct);

    public Task<AdminSaveResponse> DeleteRowAsync(
        AdminTarget target, string key, string id, CancellationToken ct = default) =>
        SendAsync<AdminSaveResponse>(
            target, HttpMethod.Delete, $"collections/{Uri.EscapeDataString(key)}/{Uri.EscapeDataString(id)}", null, ct);

    public async Task<AdminDriftResponse> GetDriftAsync(AdminTarget target, CancellationToken ct = default) {
        try {
            return await SendAsync<AdminDriftResponse>(target, HttpMethod.Get, "drift", null, ct);
        } catch (Exception e) when (e is HttpRequestException or TimeoutException or TaskCanceledException) {
            return new AdminDriftResponse { App = target.App, Available = false, Unavailable = e.Message };
        }
    }

    public Task<AdminSaveResponse> RestartAsync(AdminTarget target, CancellationToken ct = default) =>
        SendAsync<AdminSaveResponse>(target, HttpMethod.Post, "restart", null, ct);

    private async Task<T> SendAsync<T>(
        AdminTarget target, HttpMethod method, string path, HttpContent? content, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(target);

        var prefix = target.Prefix.Trim('/');
        var baseText = target.BaseUrl.AbsoluteUri.TrimEnd('/');
        using var request = new HttpRequestMessage(method, $"{baseText}/{prefix}/{path}") { Content = content };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {target.Secret}");

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        if (!response.IsSuccessStatusCode) throw Failure(target, method, path, response);

        return await response.Content.ReadFromJsonAsync<T>(Json, ct)
            ?? throw new HttpRequestException($"{target.App} returned an empty body for {method} {path}");
    }

    private static HttpRequestException Failure(
        AdminTarget target, HttpMethod method, string path, HttpResponseMessage response) {
        var code = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var detail = response.StatusCode == HttpStatusCode.Unauthorized
            ? " (the admin secret for this app is wrong or missing)"
            : "";
        return new HttpRequestException(
            $"{target.App} returned {code} for {method} {path}{detail}", null, response.StatusCode);
    }
}
