using System.Text.Json;

namespace EggIdentity.UI;

public sealed class ViewerStore<T>(ViewerCookies cookies, string name, Func<T> empty, Func<T, T>? trim = null) where T : class {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public T Load() {
        var raw = cookies.Get(name);
        if (string.IsNullOrEmpty(raw)) return empty();
        try {
            return JsonSerializer.Deserialize<T>(raw, Json) ?? empty();
        } catch (JsonException) {
            return empty();
        }
    }

    public bool Save(T value) {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value, Json);
        while (json.Length > ViewerCookieOptions.MaxValueLength) {
            if (trim is null) return false;
            var smaller = trim(value);
            var next = JsonSerializer.Serialize(smaller, Json);
            if (next.Length >= json.Length) return false;
            value = smaller;
            json = next;
        }
        cookies.Set(name, json);
        return true;
    }
}
