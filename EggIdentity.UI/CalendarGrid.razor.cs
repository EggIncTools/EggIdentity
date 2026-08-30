using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed partial class CalendarGrid<TItem> : IAsyncDisposable {
    private ElementReference _viewport;
    private DotNetObjectReference<CalendarGrid<TItem>>? _selfRef;
    private bool _initialized;

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            _selfRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("calendarGridInit", _viewport, _selfRef);
            _initialized = true;
        }
    }

    public async Task ResetScrollAsync() {
        if (!_initialized) return;
        try {
            await JS.InvokeVoidAsync("calendarGridReset", _viewport);
        }
        catch (JSDisconnectedException) {
        }
    }

    [JSInvokable]
    public async Task CommitScrollPan(int direction) {
        await OnCommitScrollPan.InvokeAsync(direction);
    }

    public async ValueTask DisposeAsync() {
        if (_initialized) {
            try {
                await JS.InvokeVoidAsync("calendarGridDestroy", _viewport);
            }
            catch (JSDisconnectedException) {
            }
        }
        _selfRef?.Dispose();
    }
}
