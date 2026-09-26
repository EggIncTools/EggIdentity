using EggIdentity.Contract;

namespace EggIdentity.Host;

public sealed record ReconcileResult(
    bool Applied,
    IReadOnlyList<(string Provider, LinkOutcome Outcome)> Outcomes,
    IReadOnlyList<Models.Identity> Removed,
    IReadOnlyList<Guid> MergedUserIds) {
    public static ReconcileResult Skipped { get; } = new(false, [], [], []);
}

public sealed record IdentityDiff(
    IReadOnlyList<AuthentikSourceConnection> ToAdd,
    IReadOnlyList<Models.Identity> ToRemove);

public sealed class IdentityReconciler(IAuthentikAdminClient authentik, IdentityResolver resolver, ProfileService profiles) {
    public async Task<ReconcileResult> ReconcileAsync(Guid userId, CancellationToken ct) {
        var identities = await profiles.ListIdentitiesAsync(userId, ct);
        var authentikRow = identities.FirstOrDefault(i => i.Provider == "authentik");
        if (authentikRow is null || string.IsNullOrEmpty(authentikRow.Username)) return ReconcileResult.Skipped;
        return await ReconcileAsync(userId, authentikRow.Username, authentikRow.Subject, identities, ct);
    }

    public async Task<ReconcileResult> ReconcileAsync(Guid userId, string authentikUsername, string subject, CancellationToken ct) {
        var identities = await profiles.ListIdentitiesAsync(userId, ct);
        return await ReconcileAsync(userId, authentikUsername, subject, identities, ct);
    }

    private async Task<ReconcileResult> ReconcileAsync(
        Guid userId, string authentikUsername, string subject, IReadOnlyList<Models.Identity> local, CancellationToken ct) {
        var userPk = await authentik.FindUserPkAsync(authentikUsername, subject, ct);
        if (userPk is null) {
            Console.Error.WriteLine($"identity reconcile for {userId}: no authentik user named {authentikUsername} matches sub");
            return ReconcileResult.Skipped;
        }

        var remote = await authentik.ListConnectionsAsync(userPk, ct);
        var diff = ComputeDiff(local, remote);

        foreach (var stale in diff.ToRemove)
            await profiles.DeleteIdentityAsync(userId, stale.Provider, stale.Subject, ct);

        var outcomes = new List<(string, LinkOutcome)>();
        var merged = new List<Guid>();
        var currentUserId = userId;
        foreach (var connection in diff.ToAdd) {
            var provider = connection.Provider!;
            var discordId = provider == IdentityWire.Discord ? connection.Identifier : null;
            var outcome = await resolver.TryLinkAsync(currentUserId, provider, connection.Identifier, discordId, null, null, ct);
            if (outcome.Conflict && outcome.ConflictUserId is { } other) {
                var kept = await resolver.MergeOldestAsync(currentUserId, other, ct);
                merged.Add(kept == currentUserId ? other : currentUserId);
                currentUserId = kept;
                outcome = await resolver.TryLinkAsync(currentUserId, provider, connection.Identifier, discordId, null, null, ct);
            }
            outcomes.Add((provider, outcome));
        }

        return new ReconcileResult(true, outcomes, diff.ToRemove, merged);
    }

    public async Task<bool> UnlinkAsync(Guid userId, string provider, string subject, CancellationToken ct) {
        var identities = await profiles.ListIdentitiesAsync(userId, ct);
        var authentikRow = identities.FirstOrDefault(i => i.Provider == "authentik");
        if (authentikRow is null || string.IsNullOrEmpty(authentikRow.Username)) return false;

        var userPk = await authentik.FindUserPkAsync(authentikRow.Username, authentikRow.Subject, ct);
        if (userPk is null) return false;

        var remote = await authentik.ListConnectionsAsync(userPk, ct);
        var match = remote.FirstOrDefault(c => c.Provider == provider && c.Identifier == subject);
        if (match is not null) await authentik.DeleteConnectionAsync(match.Pk, ct);
        return true;
    }

    public static IdentityDiff ComputeDiff(IReadOnlyList<Models.Identity> local, IReadOnlyList<AuthentikSourceConnection> remote) {
        var desired = remote
            .Where(c => c.Provider is not null)
            .GroupBy(c => c.Provider!)
            .Select(g => g.OrderBy(c => c.Pk).First())
            .ToList();
        var desiredKeys = desired.Select(c => (c.Provider!, c.Identifier)).ToHashSet();

        var toRemove = local
            .Where(i => IdentityWire.KnownProviders.Contains(i.Provider))
            .Where(i => !desiredKeys.Contains((i.Provider, i.Subject)))
            .ToList();
        var localKeys = local.Select(i => (i.Provider, i.Subject)).ToHashSet();
        var toAdd = desired.Where(c => !localKeys.Contains((c.Provider!, c.Identifier))).ToList();
        return new IdentityDiff(toAdd, toRemove);
    }
}
