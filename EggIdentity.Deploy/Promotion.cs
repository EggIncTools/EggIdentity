namespace EggIdentity.Deploy;

public sealed record PromotionPlan(string App, string FromTag, string ToTag, DeployApp Target) {
    public bool IsNoOp => string.Equals(FromTag, ToTag, StringComparison.Ordinal);
}

public static class Promotion {
    public static PromotionPlan? Plan(IEnumerable<DeployApp> apps, string name) {
        ArgumentNullException.ThrowIfNull(apps);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var rows = apps.Where(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var source = rows.FirstOrDefault(a => a.TracksLatest);
        var target = rows.FirstOrDefault(a => !a.TracksLatest);
        if (source is null || target is null) return null;

        return new PromotionPlan(target.Name, target.ResolvedTag, source.ResolvedTag, target);
    }

    public static DeployApp Apply(PromotionPlan plan) {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.IsNoOp ? plan.Target : plan.Target.WithTag(plan.ToTag);
    }

    public static IReadOnlyDictionary<string, string?> Row(DeployApp app) {
        ArgumentNullException.ThrowIfNull(app);
        return new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["tag"] = app.ResolvedTag,
            ["previous_tag"] = app.PreviousTag,
        };
    }
}
