using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace EggIdentity.UI;

public sealed class PathRouteSync(NavigationManager nav, string prefix) : IDisposable {
    private readonly string _prefix = Normalize(prefix);
    private string _currentPath = string.Empty;
    private bool _listening;

    public IReadOnlyList<string> Segments { get; private set; } = [];

    public event Action? Changed;

    public void Start() {
        SetPath(nav.ToBaseRelativePath(nav.Uri));
        if (_listening) return;
        nav.LocationChanged += OnLocationChanged;
        _listening = true;
    }

    public void Push(string path) => Navigate(path, replace: false);

    public void Replace(string path) => Navigate(path, replace: true);

    private void Navigate(string path, bool replace) {
        if (Normalize(path) == _currentPath) return;
        SetPath(path);
        nav.NavigateTo(path, new NavigationOptions { ReplaceHistoryEntry = replace });
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) {
        var path = Normalize(nav.ToBaseRelativePath(e.Location));
        if (path == _currentPath || !Owns(path)) return;
        SetPath(path);
        Changed?.Invoke();
    }

    private bool Owns(string path) =>
        _prefix.Length == 0 || path.Equals(_prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(_prefix + "/", StringComparison.OrdinalIgnoreCase);

    private void SetPath(string path) {
        _currentPath = Normalize(path);
        Segments = _currentPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string Normalize(string raw) => raw.Split('?', '#')[0].Trim('/');

    public void Dispose() {
        if (_listening) nav.LocationChanged -= OnLocationChanged;
        _listening = false;
    }
}
