namespace EggIdentity.Settings;

public sealed class SettingsRegistry {
    private readonly Dictionary<string, SettingDescriptor> _byKey = [];
    private readonly List<SettingDescriptor> _ordered = [];

    public SettingsRegistry(IEnumerable<ISettingsProvider> providers) {
        ArgumentNullException.ThrowIfNull(providers);
        foreach (var provider in providers) {
            foreach (var d in provider.Describe()) {
                if (!_byKey.TryAdd(d.Key, d)) {
                    throw new InvalidOperationException(
                        $"duplicate setting key \"{d.Key}\" from {provider.GetType().Name}");
                }
                _ordered.Add(d);
            }
        }
    }

    public IReadOnlyList<SettingDescriptor> All => _ordered;

    public SettingDescriptor? Find(string key) => _byKey.GetValueOrDefault(key);

    public SettingDescriptor Require(string key) =>
        Find(key) ?? throw new KeyNotFoundException($"unknown setting key \"{key}\"");

    public IReadOnlyList<string> Categories =>
        [.. _ordered.Select(d => d.Category).Distinct().Order(StringComparer.Ordinal)];

    public IReadOnlyList<SettingDescriptor> InCategory(string category) =>
        [.. _ordered.Where(d => string.Equals(d.Category, category, StringComparison.Ordinal))];

    public IReadOnlyList<SettingDescriptor> WithTier(ApplyTier tier) =>
        [.. _ordered.Where(d => d.Tier == tier)];
}
