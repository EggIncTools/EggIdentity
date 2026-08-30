using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace EggIdentity.UI;

public sealed class PathRouteSync(IJSRuntime js, NavigationManager nav, string prefix) : IAsyncDisposable {
    private DotNetObjectReference<PathRouteSync>? _dotNetRef;
    private string _currentPath = string.Empty;

    public IReadOnlyList<string> Segments { get; private set; } = [];

    public event Action? Changed;

    public async Task StartAsync() {
        SetPath(nav.ToBaseRelativePath(nav.Uri));
        _dotNetRef = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("pathRouteSyncListen", prefix, _dotNetRef);
    }

    public async Task Push(string path) => await Navigate(path, replace: false);

    public async Task Replace(string path) => await Navigate(path, replace: true);

    private async Task Navigate(string path, bool replace) {
        if (Normalize(path) == _currentPath) return;
        await js.InvokeVoidAsync("pathRouteSyncPush", path, replace);
        SetPath(path);
    }

    [JSInvokable]
    public void OnPathChanged(string path) {
        SetPath(path);
        Changed?.Invoke();
    }

    private void SetPath(string path) {
        _currentPath = Normalize(path);
        Segments = _currentPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string Normalize(string raw) => raw.Split('?', '#')[0].Trim('/');

    public async ValueTask DisposeAsync() {
        try {
            await js.InvokeVoidAsync("pathRouteSyncUnlisten", prefix);
        } catch (JSDisconnectedException) {
        }

        _dotNetRef?.Dispose();
    }
}
