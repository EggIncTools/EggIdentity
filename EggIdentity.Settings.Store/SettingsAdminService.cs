using System.Collections.Concurrent;

namespace EggIdentity.Settings.Store;

public sealed record SettingRow(
    SettingDescriptor Descriptor,
    string? Display,
    SettingSource Source,
    bool PendingRestart);

public sealed record SettingsSaveResult(bool Ok, string? Error, bool RestartRequired);

public sealed class SettingsAdminService(SettingsRegistry registry, SettingsStore store, SettingsCache cache) {
    public const string SecretMask = "********";

    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.Ordinal);

    public IReadOnlyList<CollectionDescriptor> Collections => registry.Collections;

    public IReadOnlyList<string> PendingRestartKeys => [.. _pending.Keys.Order(StringComparer.Ordinal)];

    public bool HasPendingRestart => !_pending.IsEmpty;

    public void ClearPendingRestart() => _pending.Clear();

    public async Task<IReadOnlyList<SettingRow>> GetRowsAsync(CancellationToken ct = default) {
        var snapshot = await cache.GetAsync(ct);
        return [.. snapshot.All().Select(v =>
            new SettingRow(v.Descriptor, v.Display, v.Source, _pending.ContainsKey(v.Key)))];
    }

    public async Task<IReadOnlyList<CollectionRow>> GetRowsAsync(string collectionKey, CancellationToken ct = default) {
        var descriptor = registry.RequireCollection(collectionKey);
        var rows = await store.ListRowsAsync(collectionKey, ct);
        return [.. rows.Select(r => Mask(descriptor, r))];
    }

    public async Task<IReadOnlyList<SettingRow>> GetCategoryAsync(string category, CancellationToken ct = default) {
        var rows = await GetRowsAsync(ct);
        return [.. rows.Where(r => string.Equals(r.Descriptor.Category, category, StringComparison.Ordinal))];
    }

    public async Task<SettingsSaveResult> SaveAsync(
        string key, string? value, string? updatedBy, CancellationToken ct = default) {
        var descriptor = registry.Find(key);
        if (descriptor is null) return new SettingsSaveResult(false, $"unknown setting \"{key}\"", false);

        if (descriptor.Kind is SettingKind.ReadOnly or SettingKind.External) {
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

    public async Task<SettingsSaveResult> CreateRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values,
        string? updatedBy, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(values);
        var descriptor = registry.FindCollection(collectionKey);
        if (descriptor is null) return new SettingsSaveResult(false, $"unknown collection \"{collectionKey}\"", false);
        if (SecretsUnavailable(descriptor) is { } unavailable) return unavailable;

        if (await store.GetRowAsync(collectionKey, id, ct) is not null) {
            return new SettingsSaveResult(false, $"{descriptor.Label} \"{id}\" already exists", false);
        }

        var merged = new Dictionary<string, string?>(values, StringComparer.Ordinal) {
            [descriptor.IdField] = id,
        };
        return await WriteRowAsync(descriptor, id, merged, updatedBy, ct);
    }

    public async Task<SettingsSaveResult> SaveRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values,
        string? updatedBy, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(values);
        var descriptor = registry.FindCollection(collectionKey);
        if (descriptor is null) return new SettingsSaveResult(false, $"unknown collection \"{collectionKey}\"", false);
        if (SecretsUnavailable(descriptor) is { } unavailable) return unavailable;

        var merged = new Dictionary<string, string?>(values, StringComparer.Ordinal) {
            [descriptor.IdField] = id,
        };
        if (descriptor.HasSecrets) {
            var existing = await store.GetRowAsync(collectionKey, id, ct);
            foreach (var field in descriptor.Fields.Where(f => f.IsSecret)) {
                var incoming = merged.GetValueOrDefault(field.Name);
                if (string.IsNullOrEmpty(incoming) || string.Equals(incoming, SecretMask, StringComparison.Ordinal))
                    merged[field.Name] = existing?.Get(field.Name);
            }
        }
        return await WriteRowAsync(descriptor, id, merged, updatedBy, ct);
    }

    private SettingsSaveResult? SecretsUnavailable(CollectionDescriptor descriptor) =>
        descriptor.HasSecrets && !store.CanStoreSecrets
            ? new SettingsSaveResult(false, "secret storage is unavailable: EGGIDENTITY_SETTINGS_KEY is not configured", false)
            : null;

    private async Task<SettingsSaveResult> WriteRowAsync(
        CollectionDescriptor descriptor, string id, IReadOnlyDictionary<string, string?> merged,
        string? updatedBy, CancellationToken ct) {
        if (SettingsValidation.ValidateRow(descriptor, merged) is string error) {
            return new SettingsSaveResult(false, error, false);
        }

        await store.UpsertRowAsync(descriptor, id, merged, updatedBy, ct);
        await cache.RefreshAsync(ct);

        return Applied(descriptor);
    }

    public async Task<SettingsSaveResult> DeleteRowAsync(string collectionKey, string id, CancellationToken ct = default) {
        var descriptor = registry.FindCollection(collectionKey);
        if (descriptor is null) return new SettingsSaveResult(false, $"unknown collection \"{collectionKey}\"", false);

        await store.DeleteRowAsync(collectionKey, id, ct);
        await cache.RefreshAsync(ct);

        return Applied(descriptor);
    }

    public async Task<DriftReport> DriftAsync(IEnumerable<EnvKeyInfo> env, CancellationToken ct = default) {
        var snapshot = await cache.GetAsync(ct);
        var databaseKeys = snapshot.All()
            .Where(v => v.Source == SettingSource.Database)
            .Select(v => v.Key)
            .ToHashSet(StringComparer.Ordinal);
        return DriftReport.Compare(registry, env, databaseKeys);
    }

    private SettingsSaveResult Applied(CollectionDescriptor descriptor) {
        var restart = descriptor.Tier == ApplyTier.RestartRequired;
        if (restart) _pending[descriptor.Key] = 0;
        return new SettingsSaveResult(true, null, restart);
    }

    private static CollectionRow Mask(CollectionDescriptor descriptor, CollectionRow row) {
        if (!descriptor.HasSecrets) return row;
        var values = new Dictionary<string, string?>(row.Values, StringComparer.Ordinal);
        foreach (var field in descriptor.Fields.Where(f => f.IsSecret)) {
            if (!string.IsNullOrEmpty(values.GetValueOrDefault(field.Name))) values[field.Name] = SecretMask;
        }
        return row with { Values = values };
    }
}
