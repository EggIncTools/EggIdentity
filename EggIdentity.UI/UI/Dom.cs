using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class Dom(IJSRuntime js) : IAsyncDisposable {
    private const string Module = "./_content/EggIdentity.UI/dom.js";

    private IJSObjectReference? _module;

    public async Task<string?> InsertAtCaretAsync(ElementReference input, string text) {
        try {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", Module);
            return await _module.InvokeAsync<string>("insertAtCaret", input, text);
        } catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException) {
            return null;
        }
    }

    public async Task ScrollToAsync(ElementReference element, double top, double left = 0, bool smooth = false) {
        try {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", Module);
            await _module.InvokeVoidAsync("scrollTo", element, top, left, smooth);
        } catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException) {
        }
    }

    public async ValueTask DisposeAsync() {
        try {
            if (_module is not null) await _module.DisposeAsync();
        } catch (JSDisconnectedException) {
        }
    }
}
