using System.Text.Json;
using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class BrowserTimeZone(IJSRuntime js, ViewerCookies cookies) {
    public const string CookieName = "tz";

    public TimeZoneInfo Zone { get; private set; } = Resolve(cookies.Get(CookieName)) ?? TimeZoneInfo.Utc;

    public event Action? Changed;

    public async Task SyncAsync(string? preferred = null) {
        if (Resolve(preferred) is { } zone) {
            Apply(zone.Id);
            return;
        }
        try {
            await using var format = await js.InvokeAsync<IJSObjectReference>("Intl.DateTimeFormat");
            var options = await format.InvokeAsync<JsonElement>("resolvedOptions");
            Apply(options.TryGetProperty("timeZone", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null);
        } catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException) {
        }
    }

    public void Apply(string? id) {
        if (Resolve(id) is not { } zone) return;
        cookies.Set(CookieName, zone.Id);
        if (zone.Id == Zone.Id) return;
        Zone = zone;
        Changed?.Invoke();
    }

    internal static TimeZoneInfo? Resolve(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;
}
