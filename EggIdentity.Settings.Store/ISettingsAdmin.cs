namespace EggIdentity.Settings.Store;

public interface ISettingsAdmin {
    string App { get; }

    bool CanRestart { get; }

    Task<IReadOnlyList<SettingRow>> GetRowsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CollectionDescriptor>> GetCollectionsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CollectionRow>> GetRowsAsync(string collectionKey, CancellationToken ct = default);

    Task<SettingsSaveResult> SaveAsync(string key, string? value, string? updatedBy, CancellationToken ct = default);

    Task<SettingsSaveResult> CreateRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy,
        CancellationToken ct = default);

    Task<SettingsSaveResult> SaveRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy,
        CancellationToken ct = default);

    Task<SettingsSaveResult> DeleteRowAsync(string collectionKey, string id, CancellationToken ct = default);

    Task<DriftReport?> DriftAsync(CancellationToken ct = default);

    Task<IReadOnlyList<string>> PendingRestartKeysAsync(CancellationToken ct = default);

    Task<SettingsSaveResult> RestartAsync(CancellationToken ct = default);

    void ClearPendingRestart();
}

public sealed class LocalSettingsAdmin(
    string app, SettingsAdminService admin, IEnvSource? env = null, IRestartTrigger? restart = null) : ISettingsAdmin {
    public string App => app;

    public bool CanRestart => restart is not null;

    public Task<IReadOnlyList<SettingRow>> GetRowsAsync(CancellationToken ct = default) => admin.GetRowsAsync(ct);

    public Task<IReadOnlyList<CollectionDescriptor>> GetCollectionsAsync(CancellationToken ct = default) =>
        Task.FromResult(admin.Collections);

    public Task<IReadOnlyList<CollectionRow>> GetRowsAsync(string collectionKey, CancellationToken ct = default) =>
        admin.GetRowsAsync(collectionKey, ct);

    public Task<SettingsSaveResult> SaveAsync(
        string key, string? value, string? updatedBy, CancellationToken ct = default) =>
        admin.SaveAsync(key, value, updatedBy, ct);

    public Task<SettingsSaveResult> CreateRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy,
        CancellationToken ct = default) =>
        admin.CreateRowAsync(collectionKey, id, values, updatedBy, ct);

    public Task<SettingsSaveResult> SaveRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy,
        CancellationToken ct = default) =>
        admin.SaveRowAsync(collectionKey, id, values, updatedBy, ct);

    public Task<SettingsSaveResult> DeleteRowAsync(string collectionKey, string id, CancellationToken ct = default) =>
        admin.DeleteRowAsync(collectionKey, id, ct);

    public async Task<DriftReport?> DriftAsync(CancellationToken ct = default) {
        if (env is null) return null;
        return await admin.DriftAsync(await env.GetAsync(ct), ct);
    }

    public Task<IReadOnlyList<string>> PendingRestartKeysAsync(CancellationToken ct = default) =>
        Task.FromResult(admin.PendingRestartKeys);

    public async Task<SettingsSaveResult> RestartAsync(CancellationToken ct = default) {
        if (restart is null) return new SettingsSaveResult(false, "restart is not available for this app", false);
        var failure = await restart.RestartAsync(ct);
        return new SettingsSaveResult(failure is null, failure, false);
    }

    public void ClearPendingRestart() => admin.ClearPendingRestart();
}
