using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace EggIdentity.UI;

public sealed class PathRouteSync(NavigationManager nav, string prefix) : IDisposable {
    private readonly string _prefix = Normalize(prefix);
    private string _currentPath = string.Empty;
    private bool _listening;

    public IReadOnlyList<string> Segments { get; private set; } = [];

    public string Fragment { get; private set; } = string.Empty;

    public event Action? Changed;

    public void Start() {
        SetLocation(nav.ToBaseRelativePath(nav.Uri));
        if (_listening) return;
        nav.LocationChanged += OnLocationChanged;
        _listening = true;
    }

    public void Push(string path) => Navigate(path, replace: false);

    public void Replace(string path) => Navigate(path, replace: true);

    public void PushFragment(string fragment) => Navigate(WithFragment(fragment), replace: false);

    public void ReplaceFragment(string fragment) => Navigate(WithFragment(fragment), replace: true);

    private string WithFragment(string fragment) =>
        fragment.Length == 0 ? "/" + _currentPath : $"/{_currentPath}#{Uri.EscapeDataString(fragment)}";

    private void Navigate(string path, bool replace) {
        if (Normalize(path) == _currentPath && FragmentOf(path) == Fragment) return;
        SetLocation(path);
        nav.NavigateTo(path, new NavigationOptions { ReplaceHistoryEntry = replace });
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) {
        var relative = nav.ToBaseRelativePath(e.Location);
        var path = Normalize(relative);
        if ((path == _currentPath && FragmentOf(relative) == Fragment) || !Owns(path)) return;
        SetLocation(relative);
        Changed?.Invoke();
    }

    private bool Owns(string path) =>
        _prefix.Length == 0 || path.Equals(_prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(_prefix + "/", StringComparison.OrdinalIgnoreCase);

    private void SetLocation(string relative) {
        _currentPath = Normalize(relative);
        Segments = _currentPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Fragment = FragmentOf(relative);
    }

    private static string Normalize(string raw) => raw.Split('?', '#')[0].Trim('/');

    private static string FragmentOf(string raw) => raw.IndexOf('#') is var i and >= 0 ? Uri.UnescapeDataString(raw[(i + 1)..]) : string.Empty;

    public void Dispose() {
        if (_listening) nav.LocationChanged -= OnLocationChanged;
        _listening = false;
    }
}
