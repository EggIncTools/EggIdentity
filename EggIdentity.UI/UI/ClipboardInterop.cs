using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class ClipboardInterop(IJSRuntime js) {
    public async Task<bool> WriteAsync(string text) {
        try {
            await js.InvokeVoidAsync("navigator.clipboard.writeText", text);
            return true;
        } catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException) {
            return false;
        }
    }
}
