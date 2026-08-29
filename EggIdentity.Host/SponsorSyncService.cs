using EggIdentity;
using EggIdentity.Models;

namespace EggIdentity.Host;

public sealed class SponsorSyncService(
    SponsorConfig config,
    IGitHubSponsorClient githubClient,
    IDiscordRoleClient discordClient,
    GitHubSponsorStatusStore store,
    ProfileService profiles,
    UserQueries users) {

    public async Task<GitHubSponsorStatus?> SyncAsync(Guid userId, CancellationToken ct) {
        var identities = await profiles.ListIdentitiesAsync(userId, ct);
        var github = identities.FirstOrDefault(i => i.Provider == "github");
        if (github is null) return null;

        var isSponsor = await githubClient.IsSponsoredByUserIdAsync(github.Subject, ct);
        await store.UpsertAsync(userId, isSponsor, ct);
        await ReconcileRoleAsync(userId, isSponsor, ct);
        return await store.GetAsync(userId, ct);
    }

    public async Task ApplyWebhookEventAsync(string githubSubject, bool isSponsor, CancellationToken ct) {
        var userId = await store.FindUserIdByGitHubSubjectAsync(githubSubject, ct);
        if (userId is null) return;

        await store.UpsertAsync(userId.Value, isSponsor, ct);
        await ReconcileRoleAsync(userId.Value, isSponsor, ct);
    }

    public async Task ReconcileRoleAsync(Guid userId, CancellationToken ct) {
        var status = await store.GetAsync(userId, ct);
        if (status is null) return;
        await ReconcileRoleAsync(userId, status.IsSponsor, ct);
    }

    private async Task ReconcileRoleAsync(Guid userId, bool isSponsor, CancellationToken ct) {
        var user = await users.GetAsync(userId, ct);
        if (user?.DiscordId is null) return;

        if (isSponsor)
            await discordClient.AddRoleAsync(config.DiscordGuildId, user.DiscordId, config.DiscordRoleId, ct);
        else
            await discordClient.RemoveRoleAsync(config.DiscordGuildId, user.DiscordId, config.DiscordRoleId, ct);
    }
}
