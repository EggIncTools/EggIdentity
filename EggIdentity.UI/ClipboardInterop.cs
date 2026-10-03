using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class ClipboardInterop(IJSRuntime js) {
    public async Task<bool> WriteAsync(string text) {
        if (!await UiScript.EnsureAsync(js, UiScript.Clipboard)) return false;
        try {
            return await js.InvokeAsync<bool>("eggClipboardWrite", text);
        } catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException) {
            return false;
        }
    }
}
