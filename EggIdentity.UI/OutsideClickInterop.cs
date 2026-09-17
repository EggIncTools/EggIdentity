using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class OutsideClickInterop(IJSRuntime js) {
    public async Task RegisterAsync<T>(string id, ElementReference element, DotNetObjectReference<T> dotNetRef) where T : class {
        try {
            await js.InvokeVoidAsync("outsideClickRegister", id, element, dotNetRef);
        } catch (JSDisconnectedException) {
        }
    }

    public async Task UnregisterAsync(string id) {
        try {
            await js.InvokeVoidAsync("outsideClickUnregister", id);
        } catch (JSDisconnectedException) {
        }
    }
}
