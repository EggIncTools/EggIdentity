using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace EggIdentity.UI;

public sealed class PathRouteSync(NavigationManager nav) : IDisposable {
    private event Action? _changed;

    public event Action? Changed {
        add {
            if (_changed is null) nav.LocationChanged += OnLocationChanged;
            _changed += value;
        }
        remove {
            _changed -= value;
            if (_changed is null) nav.LocationChanged -= OnLocationChanged;
        }
    }

    public IReadOnlyList<string> Segments =>
        Normalize(nav.ToBaseRelativePath(nav.Uri)).Split('/', StringSplitOptions.RemoveEmptyEntries);

    public void Push(string path) => Navigate(path, replace: false);

    public void Replace(string path) => Navigate(path, replace: true);

    private void Navigate(string path, bool replace) {
        if (Normalize(path) == Normalize(nav.ToBaseRelativePath(nav.Uri))) return;
        nav.NavigateTo(path, replace: replace);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => _changed?.Invoke();

    private static string Normalize(string raw) => raw.Split('?', '#')[0].Trim('/');

    public void Dispose() => nav.LocationChanged -= OnLocationChanged;
}
