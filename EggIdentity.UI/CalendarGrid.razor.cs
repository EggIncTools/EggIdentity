using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed partial class CalendarGrid<TItem> : IAsyncDisposable {
    private ElementReference _viewport;
    private DotNetObjectReference<CalendarGrid<TItem>>? _selfRef;
    private bool _initialized;
    private bool _commitPending;
    private int _commitGeneration;
    private IReadOnlyList<PeriodSlot<TItem>>? _committedPeriods;

    protected override void OnParametersSet() {
        if (_commitPending && !ReferenceEquals(Periods, _committedPeriods)) {
            _commitPending = false;
            _commitGeneration++;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (firstRender) {
            _selfRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("calendarGridInit", _viewport, _selfRef);
            _initialized = true;
        }
    }

    public async Task ResetScrollAsync() {
        if (!_initialized) return;
        _commitPending = false;
        try {
            await JS.InvokeVoidAsync("calendarGridReset", _viewport);
        } catch (JSDisconnectedException) {
        }
    }

    [JSInvokable]
    public async Task CommitScrollPan(int direction) {
        _commitPending = true;
        _committedPeriods = Periods;
        await OnCommitScrollPan.InvokeAsync(direction);
    }

    public async ValueTask DisposeAsync() {
        if (_initialized) {
            try {
                await JS.InvokeVoidAsync("calendarGridDestroy", _viewport);
            } catch (JSDisconnectedException) {
            }
        }
        _selfRef?.Dispose();
    }
}
