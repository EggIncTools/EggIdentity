using EggIdentity.Contract;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;

namespace EggIdentity.Settings.Api;

public sealed class RemoteSettingsAdmin(AdminApiClient client, AdminTarget target) : ISettingsAdmin {
    public string App => target.App;

    public bool CanRestart => true;

    public async Task<IReadOnlyList<SettingRow>> GetRowsAsync(CancellationToken ct = default) {
        var response = await client.GetSettingsAsync(target, ct);
        return [.. response.Settings.Select(AdminWireMapping.FromWire)];
    }

    public async Task<IReadOnlyList<CollectionDescriptor>> GetCollectionsAsync(CancellationToken ct = default) {
        var wires = await client.GetCollectionsAsync(target, ct);
        return [.. wires.Select(AdminWireMapping.FromWire)];
    }

    public async Task<IReadOnlyList<CollectionRow>> GetRowsAsync(
        string collectionKey, CancellationToken ct = default) {
        var response = await client.GetCollectionAsync(target, collectionKey, ct);
        return [.. response.Rows.Select(AdminWireMapping.FromWire)];
    }

    public async Task<SettingsSaveResult> SaveAsync(
        string key, string? value, string? updatedBy, CancellationToken ct = default) =>
        AdminWireMapping.FromWire(await client.SaveAsync(target, key, value, updatedBy, ct));

    public async Task<SettingsSaveResult> CreateRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy,
        CancellationToken ct = default) =>
        AdminWireMapping.FromWire(await client.CreateRowAsync(
            target, collectionKey, new AdminRowRequest { Id = id, Values = values, UpdatedBy = updatedBy }, ct));

    public async Task<SettingsSaveResult> SaveRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy,
        CancellationToken ct = default) =>
        AdminWireMapping.FromWire(await client.SaveRowAsync(
            target, collectionKey, id, new AdminRowRequest { Id = id, Values = values, UpdatedBy = updatedBy }, ct));

    public async Task<SettingsSaveResult> DeleteRowAsync(
        string collectionKey, string id, CancellationToken ct = default) =>
        AdminWireMapping.FromWire(await client.DeleteRowAsync(target, collectionKey, id, ct));

    public async Task<DriftReport?> DriftAsync(CancellationToken ct = default) {
        var response = await client.GetDriftAsync(target, ct);
        return response.Available ? AdminWireMapping.FromWire(response) : null;
    }

    public async Task<IReadOnlyList<string>> PendingRestartKeysAsync(CancellationToken ct = default) =>
        (await client.GetSettingsAsync(target, ct)).PendingRestartKeys;

    public async Task<SettingsSaveResult> RestartAsync(CancellationToken ct = default) =>
        AdminWireMapping.FromWire(await client.RestartAsync(target, ct));

    public void ClearPendingRestart() {
    }
}
