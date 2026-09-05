namespace EggIdentity.Settings;

public interface ISettingsSource {
    SettingValue Value(string key);
    IReadOnlyList<SettingValue> All();
}

public sealed class SettingsSnapshot : ISettingsSource {
    private readonly SettingsRegistry _registry;
    private readonly Dictionary<string, SettingValue> _values;
    private readonly Dictionary<string, IReadOnlyList<CollectionRow>> _collections;

    public SettingsSnapshot(
        SettingsRegistry registry,
        IReadOnlyDictionary<string, string?> database,
        Func<string, string?>? file = null,
        Func<string, string?>? environment = null,
        IReadOnlyDictionary<string, IReadOnlyList<CollectionRow>>? collections = null) {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(database);

        _registry = registry;
        var env = environment ?? Environment.GetEnvironmentVariable;
        _values = new Dictionary<string, SettingValue>(StringComparer.Ordinal);
        _collections = new Dictionary<string, IReadOnlyList<CollectionRow>>(StringComparer.Ordinal);

        foreach (var d in registry.All) {
            _values[d.Key] = Resolve(d, database, file, env);
        }

        foreach (var c in registry.Collections) {
            IReadOnlyList<CollectionRow> stored =
                collections is not null && collections.TryGetValue(c.Key, out var rows) ? rows : [];
            _collections[c.Key] = [.. stored.Select(r => ApplyDefaults(c, r))];
        }
    }

    public SettingValue Value(string key) =>
        _values.TryGetValue(key, out var v) ? v : throw new KeyNotFoundException($"unknown setting key \"{key}\"");

    public IReadOnlyList<SettingValue> All() => [.. _registry.All.Select(d => _values[d.Key])];

    public string? GetString(string key) => Value(key).Value;

    public bool GetBool(string key) => Value(key).AsBool();

    public int GetInt(string key, int fallback = 0) => Value(key).AsInt(fallback);

    public TimeSpan? GetDuration(string key) => Value(key).AsDuration();

    public IReadOnlyList<string> GetList(string key) => Value(key).AsList();

    public IReadOnlyList<CollectionRow> Rows(string collectionKey) =>
        _collections.TryGetValue(collectionKey, out var rows)
            ? rows
            : throw new KeyNotFoundException($"unknown collection key \"{collectionKey}\"");

    public IReadOnlyList<T> Collection<T>(string collectionKey) =>
        [.. Rows(collectionKey).Select(r => CollectionBinder.Bind<T>(r.Values))];

    private static CollectionRow ApplyDefaults(CollectionDescriptor c, CollectionRow row) {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var f in c.Fields) {
            var stored = row.Values.GetValueOrDefault(f.Name);
            values[f.Name] = string.IsNullOrEmpty(stored) ? f.Default : stored;
        }
        return row with { Values = values };
    }

    private static SettingValue Resolve(
        SettingDescriptor d,
        IReadOnlyDictionary<string, string?> database,
        Func<string, string?>? file,
        Func<string, string?> env) {
        if (d.Tier != ApplyTier.Bootstrap
            && database.TryGetValue(d.Key, out var dbValue) && !string.IsNullOrEmpty(dbValue)) {
            return new SettingValue(d, dbValue, SettingSource.Database);
        }

        var fromFile = file?.Invoke(d.EnvKey);
        if (!string.IsNullOrEmpty(fromFile)) return new SettingValue(d, fromFile, SettingSource.File);

        var fromEnv = env(d.EnvKey);
        if (!string.IsNullOrEmpty(fromEnv)) return new SettingValue(d, fromEnv, SettingSource.Environment);

        return new SettingValue(d, d.Default, SettingSource.Default);
    }
}
