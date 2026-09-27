using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class BrowserTimeZone(IJSRuntime js, IHttpContextAccessor? accessor = null) {
    public const string CookieName = "tz";

    public TimeZoneInfo Zone { get; private set; } = Resolve(accessor?.HttpContext?.Request.Cookies[CookieName]) ?? TimeZoneInfo.Utc;

    public event Action? Changed;

    public async Task SyncAsync(string? preferred = null) {
        try {
            if (Resolve(preferred) is { } zone) {
                await js.InvokeVoidAsync("eggTimeZoneWrite", zone.Id);
                Apply(zone.Id);
                return;
            }
            Apply(await js.InvokeAsync<string?>("eggTimeZoneBrowser"));
        } catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException) {
        }
    }

    public void Apply(string? id) {
        if (Resolve(id) is not { } zone || zone.Id == Zone.Id) return;
        Zone = zone;
        Changed?.Invoke();
    }

    internal static TimeZoneInfo? Resolve(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;
}
