using System.Collections.Concurrent;

namespace EggIdentity.Settings.Store;

public sealed record SettingRow(
    SettingDescriptor Descriptor,
    string? Display,
    SettingSource Source,
    bool PendingRestart);

public sealed record SettingsSaveResult(bool Ok, string? Error, bool RestartRequired);

public sealed class SettingsAdminService(SettingsRegistry registry, SettingsStore store, SettingsCache cache) {
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);

    public IReadOnlyList<string> PendingRestartKeys => [.. _pending.Keys.Order(StringComparer.Ordinal)];

    public bool HasPendingRestart => !_pending.IsEmpty;

    public void ClearPendingRestart() => _pending.Clear();

    public async Task<IReadOnlyList<SettingRow>> GetRowsAsync(CancellationToken ct = default) {
        var snapshot = await cache.GetAsync(ct);
        return [.. snapshot.All().Select(v =>
            new SettingRow(v.Descriptor, v.Display, v.Source, _pending.ContainsKey(v.Key)))];
    }

    public async Task<IReadOnlyList<SettingRow>> GetCategoryAsync(string category, CancellationToken ct = default) {
        var rows = await GetRowsAsync(ct);
        return [.. rows.Where(r => string.Equals(r.Descriptor.Category, category, StringComparison.Ordinal))];
    }

    public async Task<SettingsSaveResult> SaveAsync(
        string key, string? value, string? updatedBy, CancellationToken ct = default) {
        var descriptor = registry.Find(key);
        if (descriptor is null) return new SettingsSaveResult(false, $"unknown setting \"{key}\"", false);

        if (descriptor.Kind == SettingKind.ReadOnly) {
            return new SettingsSaveResult(false, $"{descriptor.Label} is read-only", false);
        }

        if (descriptor.Tier == ApplyTier.Bootstrap && !descriptor.AllowBootstrapEdit) {
            return new SettingsSaveResult(
                false, $"{descriptor.Label} is a bootstrap setting and must be changed on the stack", false);
        }

        if (descriptor.IsSecret && !store.CanStoreSecrets) {
            return new SettingsSaveResult(
                false, "secret storage is unavailable: EGGIDENTITY_SETTINGS_KEY is not configured", false);
        }

        if (SettingsValidation.Validate(descriptor, value) is string error) {
            return new SettingsSaveResult(false, error, false);
        }

        if (string.IsNullOrWhiteSpace(value)) {
            await store.DeleteAsync(key, ct);
        } else {
            await store.SetAsync(descriptor, value, updatedBy, ct);
        }

        await store.NotifyChangedAsync(ct);
        await cache.RefreshAsync(ct);

        var restart = descriptor.Tier == ApplyTier.RestartRequired;
        if (restart) _pending[key] = 0;

        return new SettingsSaveResult(true, null, restart);
    }

    public DriftReport Drift(IEnumerable<string> stackKeys) => DriftReport.Compare(registry, stackKeys);
}
