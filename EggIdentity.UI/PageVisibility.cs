using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class PageVisibility(IJSRuntime js) : IAsyncDisposable {
    private const string Module = "./_content/EggIdentity.UI/pageVisibility.js";

    private DotNetObjectReference<PageVisibility>? _self;
    private IJSObjectReference? _module;
    private IJSObjectReference? _watch;

    public bool Visible { get; private set; } = true;

    public event Action<bool>? Changed;

    public async Task StartAsync() {
        if (_self is not null) return;
        try {
            _self = DotNetObjectReference.Create(this);
            _module = await js.InvokeAsync<IJSObjectReference>("import", Module);
            _watch = await _module.InvokeAsync<IJSObjectReference>("watch", _self);
            OnVisibilityChanged(await _watch.InvokeAsync<bool>("visible"));
        } catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException) {
        }
    }

    [JSInvokable]
    public void OnVisibilityChanged(bool visible) {
        if (visible == Visible) return;
        Visible = visible;
        Changed?.Invoke(visible);
    }

    public async ValueTask DisposeAsync() {
        try {
            if (_watch is not null) {
                await _watch.InvokeVoidAsync("stop");
                await _watch.DisposeAsync();
            }
            if (_module is not null) await _module.DisposeAsync();
        } catch (JSDisconnectedException) {
        }
        _self?.Dispose();
    }
}
