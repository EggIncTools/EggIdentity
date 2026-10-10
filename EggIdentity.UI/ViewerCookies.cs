using Microsoft.AspNetCore.Http;

namespace EggIdentity.UI;

public sealed class ViewerCookies(ViewerCookieOptions options, IHttpContextAccessor? accessor = null) {
    private readonly Dictionary<string, string> _values = Snapshot(options, accessor?.HttpContext);
    private readonly List<string> _pending = [];

    public event Action? Changed;

    public event Action<string>? ValueChanged;

    public IReadOnlyList<string> Pending => _pending;

    internal IReadOnlyDictionary<string, string> Values => _values;

    public string? Get(string name) => _values.GetValueOrDefault(name);

    public void Set(string name, string value) {
        ArgumentNullException.ThrowIfNull(value);
        if (!options.Cookies.ContainsKey(name))
            throw new ArgumentException($"Cookie '{name}' is not allowed by ViewerCookieOptions.", nameof(name));
        if (value.Length > ViewerCookieOptions.MaxValueLength)
            throw new ArgumentException($"Cookie '{name}' value exceeds {ViewerCookieOptions.MaxValueLength} characters.", nameof(value));
        if (_values.GetValueOrDefault(name) == value) return;
        _values[name] = value;
        _pending.Add($"{options.Path}?name={Uri.EscapeDataString(name)}&value={Uri.EscapeDataString(value)}");
        Changed?.Invoke();
        ValueChanged?.Invoke(name);
    }

    private bool _persistClaimed;

    internal bool ClaimPersistence() {
        if (_persistClaimed) return false;
        _persistClaimed = true;
        return true;
    }

    internal void Restore(IReadOnlyDictionary<string, string> values) {
        foreach (var (name, value) in values)
            if (options.Cookies.ContainsKey(name)) _values.TryAdd(name, value);
    }

    internal void Written(string url) {
        if (_pending.Remove(url)) Changed?.Invoke();
    }

    private static Dictionary<string, string> Snapshot(ViewerCookieOptions options, HttpContext? ctx) {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ctx is null) return values;
        foreach (var name in options.Cookies.Keys)
            if (ctx.Request.Cookies.TryGetValue(name, out var value)) values[name] = value;
        return values;
    }
}
