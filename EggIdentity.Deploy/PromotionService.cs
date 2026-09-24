using EggIdentity.Settings;

namespace EggIdentity.Deploy;

public interface IPromotionStore {
    Task<IReadOnlyList<CollectionRow>> GetRowsAsync(string collectionKey, CancellationToken ct = default);

    Task<string?> SaveRowAsync(
        string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy,
        CancellationToken ct = default);
}

public sealed record PromotionView(
    string App, string ProdTag, string SubProdTag, string PreviousTag, bool CanPromote, bool CanRollBack) {
    public string? Blocked { get; init; }
}

public sealed class PromotionService(IPromotionStore store) {
    public async Task<IReadOnlyList<PromotionView>> ViewAsync(CancellationToken ct = default) {
        var apps = await ReadAsync(ct);
        return [.. apps
            .Where(a => !a.TracksLatest)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(prod => Describe(apps, prod))];
    }

    public async Task<string?> PromoteAsync(string app, string? updatedBy, CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);
        var rows = await store.GetRowsAsync(DeployApps.Key, ct);
        var apps = Bind(rows);
        if (Promotion.Plan(apps, app) is not { } plan) return NoPair(app);
        return plan.IsNoOp
            ? $"{app} is already running {plan.ToTag}"
            : await WriteAsync(rows, Promotion.Apply(plan), updatedBy, ct);
    }

    public async Task<string?> RollBackAsync(string app, string? updatedBy, CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);
        var rows = await store.GetRowsAsync(DeployApps.Key, ct);
        var prod = Bind(rows).FirstOrDefault(a =>
            !a.TracksLatest && string.Equals(a.Name, app, StringComparison.OrdinalIgnoreCase));
        if (prod is null) return $"{app} has no production row in {DeployApps.Key}";
        return !prod.CanRollBack
            ? $"{app} has no previous tag to roll back to"
            : await WriteAsync(rows, prod.RollBack(), updatedBy, ct);
    }

    private static PromotionView Describe(IReadOnlyList<DeployApp> apps, DeployApp prod) {
        var subProd = apps.FirstOrDefault(a =>
            a.TracksLatest && string.Equals(a.Name, prod.Name, StringComparison.OrdinalIgnoreCase));
        return new PromotionView(
            prod.Name, prod.ResolvedTag, subProd?.ResolvedTag ?? "", prod.PreviousTag,
            subProd is not null && !string.Equals(subProd.ResolvedTag, prod.ResolvedTag, StringComparison.Ordinal),
            prod.CanRollBack) {
            Blocked = subProd is null ? NoPair(prod.Name) : null,
        };
    }

    private static string NoPair(string app) =>
        $"{app} has no sub-prod row in {DeployApps.Key}, so there is nothing to promote from";

    private async Task<IReadOnlyList<DeployApp>> ReadAsync(CancellationToken ct) =>
        Bind(await store.GetRowsAsync(DeployApps.Key, ct));

    private static IReadOnlyList<DeployApp> Bind(IEnumerable<CollectionRow> rows) =>
        [.. rows.Select(r => CollectionBinder.Bind<DeployApp>(r.Values))];

    private Task<string?> WriteAsync(
        IReadOnlyList<CollectionRow> rows, DeployApp app, string? updatedBy, CancellationToken ct) {
        var existing = rows.FirstOrDefault(r =>
            string.Equals(r.Get(DeployApps.Descriptor.IdField), app.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return Task.FromResult<string?>($"{app.Name} has no row in {DeployApps.Key}");

        var values = new Dictionary<string, string?>(existing.Values, StringComparer.Ordinal);
        foreach (var (field, value) in Promotion.Row(app)) values[field] = value;
        return store.SaveRowAsync(DeployApps.Key, app.Name, values, updatedBy, ct);
    }
}
