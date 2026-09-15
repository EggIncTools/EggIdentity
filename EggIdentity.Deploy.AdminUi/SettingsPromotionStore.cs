using EggIdentity.Settings;
using EggIdentity.Settings.Store;

namespace EggIdentity.Deploy.AdminUi;

public sealed class SettingsPromotionStore(SettingsAdminService admin) : IPromotionStore {
    public Task<IReadOnlyList<CollectionRow>> GetRowsAsync(string collectionKey, CancellationToken ct = default) =>
        admin.GetRowsAsync(collectionKey, ct);

    public async Task<string?> SaveRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy,
        CancellationToken ct = default) {
        var result = await admin.SaveRowAsync(collectionKey, id, values, updatedBy, ct);
        return result.Ok ? null : result.Error ?? "the settings store rejected the write";
    }
}
