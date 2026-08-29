using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class OutsideClickInterop(IJSRuntime js) {
    public async Task RegisterAsync<T>(string id, ElementReference element, DotNetObjectReference<T> dotNetRef) where T : class {
        await js.InvokeVoidAsync("outsideClickRegister", id, element, dotNetRef);
    }

    public async Task UnregisterAsync(string id) {
        await js.InvokeVoidAsync("outsideClickUnregister", id);
    }
}
