using System.Collections.Concurrent;
using EggIdentity;

namespace EggIdentity.Host;

public sealed class SupporterStatusService(
    SponsorConfig config,
    IDiscordRoleClient discordClient,
    GitHubSponsorStatusStore sponsorStore,
    UserQueries users) {
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Guid, (bool IsSupporter, DateTimeOffset Expires)> _cache = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastRefresh = new();

    public async Task<bool> IsSupporterAsync(Guid userId, CancellationToken ct) {
        var now = DateTimeOffset.UtcNow;
        if (_cache.TryGetValue(userId, out var cached) && cached.Expires > now)
            return cached.IsSupporter;

        var result = await ComputeAsync(userId, ct);
        _cache[userId] = (result, now + CacheTtl);
        return result;
    }

    public async Task<bool?> RefreshAsync(Guid userId, CancellationToken ct) {
        var now = DateTimeOffset.UtcNow;
        if (_lastRefresh.TryGetValue(userId, out var last) && now - last < CacheTtl) return null;
        _lastRefresh[userId] = now;

        var result = await ComputeAsync(userId, ct);
        _cache[userId] = (result, now + CacheTtl);
        return result;
    }

    private async Task<bool> ComputeAsync(Guid userId, CancellationToken ct) {
        var sponsorStatus = await sponsorStore.GetAsync(userId, ct);
        if (sponsorStatus?.IsSponsor == true) return true;

        var user = await users.GetAsync(userId, ct);
        if (user?.DiscordId is null) return false;

        try {
            return await discordClient.HasRoleAsync(config.DiscordGuildId, user.DiscordId, config.DiscordRoleId, ct);
        } catch (Exception) {
            return false;
        }
    }
}
