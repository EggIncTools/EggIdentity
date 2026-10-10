namespace EggIdentity.Settings;

public sealed class SettingsRegistry {
    private readonly Dictionary<string, SettingDescriptor> _byKey = [];
    private readonly List<SettingDescriptor> _ordered = [];
    private readonly Dictionary<string, CollectionDescriptor> _collectionsByKey = [];
    private readonly List<CollectionDescriptor> _collections = [];

    public SettingsRegistry(IEnumerable<ISettingsProvider> providers) : this(providers, [], null) {
    }

    public SettingsRegistry(IEnumerable<ISettingsProvider> settings, IEnumerable<ICollectionProvider> collections)
        : this(settings, collections, null) {
    }

    private SettingsRegistry(
        IEnumerable<ISettingsProvider> settings, IEnumerable<ICollectionProvider> collections, string? frameworkKey) {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(collections);

        foreach (var provider in settings) {
            foreach (var d in provider.Describe()) {
                if (frameworkKey is not null
                    && string.Equals(d.Key, frameworkKey, StringComparison.Ordinal)
                    && _byKey.ContainsKey(d.Key)) {
                    continue;
                }
                if (!_byKey.TryAdd(d.Key, d)) {
                    throw new InvalidOperationException(
                        $"duplicate setting key \"{d.Key}\" from {provider.GetType().Name}");
                }
                _ordered.Add(d);
            }
        }

        foreach (var provider in collections) {
            foreach (var c in provider.Describe()) {
                Check(c, provider);
                if (!_collectionsByKey.TryAdd(c.Key, c)) {
                    throw new InvalidOperationException(
                        $"duplicate collection key \"{c.Key}\" from {provider.GetType().Name}");
                }
                _collections.Add(c);
            }
        }
    }

    public static SettingsRegistry Compose(
        IEnumerable<ISettingsProvider> settings, IEnumerable<ICollectionProvider> collections) {
        ArgumentNullException.ThrowIfNull(settings);
        return new SettingsRegistry(
            [SettingsFrameworkSettings.Provider, .. settings], collections, SettingsFrameworkSettings.EncryptionKey);
    }

    public IReadOnlyList<SettingDescriptor> All => _ordered;

    public IReadOnlyList<CollectionDescriptor> Collections => _collections;

    public SettingDescriptor? Find(string key) => _byKey.GetValueOrDefault(key);

    public SettingDescriptor Require(string key) =>
        Find(key) ?? throw new KeyNotFoundException($"unknown setting key \"{key}\"");

    public CollectionDescriptor? FindCollection(string key) => _collectionsByKey.GetValueOrDefault(key);

    public CollectionDescriptor RequireCollection(string key) =>
        FindCollection(key) ?? throw new KeyNotFoundException($"unknown collection key \"{key}\"");

    public IReadOnlyList<string> Categories =>
        [.. _ordered.Select(d => d.Category).Distinct().Order(StringComparer.Ordinal)];

    public IReadOnlyList<SettingDescriptor> InCategory(string category) =>
        [.. _ordered.Where(d => string.Equals(d.Category, category, StringComparison.Ordinal))];

    public IReadOnlyList<SettingDescriptor> WithTier(ApplyTier tier) =>
        [.. _ordered.Where(d => d.Tier == tier)];

    private static void Check(CollectionDescriptor c, ICollectionProvider provider) {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in c.Fields) {
            if (!names.Add(f.Name)) {
                throw new InvalidOperationException(
                    $"duplicate field \"{f.Name}\" in collection \"{c.Key}\" from {provider.GetType().Name}");
            }
        }
        if (!names.Contains(c.IdField)) {
            throw new InvalidOperationException(
                $"collection \"{c.Key}\" names id field \"{c.IdField}\" which is not a declared field");
        }
        if (c.DisplayField is not null && !names.Contains(c.DisplayField)) {
            throw new InvalidOperationException(
                $"collection \"{c.Key}\" names display field \"{c.DisplayField}\" which is not a declared field");
        }
    }
}
