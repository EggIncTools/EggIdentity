using Microsoft.JSInterop;

namespace EggIdentity.UI;

internal static class UiScript {
    internal const string Tooltip = "./_content/EggIdentity.UI/tooltip.js";
    internal const string Clipboard = "./_content/EggIdentity.UI/clipboard.js";
    internal const string Download = "./_content/EggIdentity.UI/download.js";
    internal const string OutsideClick = "./_content/EggIdentity.UI/outsideClick.js";

    internal static async Task<bool> EnsureAsync(IJSRuntime js, string path) {
        try {
            await using var module = await js.InvokeAsync<IJSObjectReference>("import", path);
            return true;
        } catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException) {
            return false;
        }
    }
}
