using System.Text;
using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class DownloadInterop(IJSRuntime js) {
    public Task<bool> SaveTextAsync(string fileName, string text, string contentType = "text/plain;charset=utf-8") =>
        SaveBytesAsync(fileName, Encoding.UTF8.GetBytes(text), contentType);

    public async Task<bool> SaveBytesAsync(string fileName, byte[] bytes, string contentType = "application/octet-stream") {
        if (!await UiScript.EnsureAsync(js, UiScript.Download)) return false;
        try {
            await js.InvokeVoidAsync("eggDownload", fileName, contentType, bytes);
            return true;
        } catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException) {
            return false;
        }
    }
}
