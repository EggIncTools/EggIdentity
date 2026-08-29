using EggIdentity;
using EggIdentity.Db;
using EggIdentity.Models;
using Npgsql;

namespace EggIdentity.Tests;

public class IdentityResolverTests {
    private static string? ConnString => Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");

    private static async Task<NpgsqlDataSource> MakeDbAsync() {
        var dataSource = NpgsqlDataSource.Create(ConnString!);
        await using var conn = await dataSource.OpenConnectionAsync();
        await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
        return dataSource;
    }

    private static IdentityResolver MakeResolver(NpgsqlDataSource db, string adminCsv = "") =>
        new(db, AdminAllowlist.FromConfig(adminCsv));

    [Fact]
    public async Task ResolveAsync_NewSub_NoDiscordId_CreatesNewUser() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var result = await resolver.ResolveAsync("authentik", "sub-new-1", null, "alice", null, CancellationToken.None);

        Assert.True(result.IsNew);
        Assert.Equal("viewer", result.Role);
    }

    [Fact]
    public async Task ResolveAsync_ExistingSub_ReturnsSameUserId() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var first = await resolver.ResolveAsync("authentik", "sub-existing-1", null, "bob", null, CancellationToken.None);
        var second = await resolver.ResolveAsync("authentik", "sub-existing-1", null, "bob", null, CancellationToken.None);

        Assert.Equal(first.UserId, second.UserId);
        Assert.False(second.IsNew);
    }

    [Fact]
    public async Task ResolveAsync_MatchingDiscordId_AutoLinksExistingUser() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var discordResult = await resolver.ResolveAsync("discord", "discord-42", null, "carol", null, CancellationToken.None);
        var linkedResult = await resolver.ResolveAsync("authentik", "sub-link-1", "discord-42", "carol", null, CancellationToken.None);

        Assert.Equal(discordResult.UserId, linkedResult.UserId);
    }

    [Fact]
    public async Task ResolveAsync_AdminAllowlist_PromotesOnLogin() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db, "999-admin");

        var result = await resolver.ResolveAsync("discord", "999-admin", null, "dave", null, CancellationToken.None);

        Assert.Equal("admin", result.Role);
    }

    [Fact]
    public async Task ResolveAsync_DiscordPath_IsTransactional_ConcurrentFirstLoginsAgree() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var tasks = Enumerable.Range(0, 5)
            .Select(_ => resolver.ResolveAsync("discord", "discord-race-1", null, "racer", null, CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        Assert.Single(results.Select(r => r.UserId).Distinct());
    }

    [Fact]
    public async Task MergeAsync_ReassignsIdentitiesAndDeletesLoser() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var keep = await resolver.ResolveAsync("discord", "keep-1", null, "keeper", null, CancellationToken.None);
        var merge = await resolver.ResolveAsync("authentik", "merge-sub-1", null, "merged", null, CancellationToken.None);

        var winner = await resolver.MergeAsync(keep.UserId, merge.UserId, CancellationToken.None);

        Assert.Equal(keep.UserId, winner);
        var users = new UserQueries(db);
        Assert.Null(await users.GetAsync(merge.UserId, CancellationToken.None));
        var reResolved = await resolver.ResolveAsync("authentik", "merge-sub-1", null, "merged", null, CancellationToken.None);
        Assert.Equal(keep.UserId, reResolved.UserId);
    }

    [Fact]
    public async Task TryLinkAsync_UnclaimedIdentity_LinksToCurrentUser() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var owner = await resolver.ResolveAsync("discord", "link-owner-1", null, "owner", null, CancellationToken.None);
        var outcome = await resolver.TryLinkAsync(owner.UserId, "authentik", "link-new-sub-1", null, "owner", null, CancellationToken.None);

        Assert.True(outcome.Linked);
        Assert.False(outcome.Conflict);
        var relinked = await resolver.ResolveAsync("authentik", "link-new-sub-1", null, "owner", null, CancellationToken.None);
        Assert.Equal(owner.UserId, relinked.UserId);
    }

    [Fact]
    public async Task TryLinkAsync_AlreadyLinkedToCurrentUser_IsIdempotent() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var owner = await resolver.ResolveAsync("discord", "link-owner-2", null, "owner2", null, CancellationToken.None);
        var first = await resolver.TryLinkAsync(owner.UserId, "authentik", "link-mine-sub-1", null, "owner2", null, CancellationToken.None);
        var second = await resolver.TryLinkAsync(owner.UserId, "authentik", "link-mine-sub-1", null, "owner2", null, CancellationToken.None);

        Assert.True(first.Linked);
        Assert.True(second.Linked);
        Assert.False(second.Conflict);
    }

    [Fact]
    public async Task TryLinkAsync_ClaimedByDifferentUser_ReturnsConflictAndDoesNotMutate() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var other = await resolver.ResolveAsync("authentik", "link-taken-sub-1", null, "taken-owner", null, CancellationToken.None);
        var requester = await resolver.ResolveAsync("discord", "link-requester-1", null, "requester", null, CancellationToken.None);

        var outcome = await resolver.TryLinkAsync(requester.UserId, "authentik", "link-taken-sub-1", null, "requester", null, CancellationToken.None);

        Assert.False(outcome.Linked);
        Assert.True(outcome.Conflict);
        Assert.Equal("taken-owner", outcome.ConflictUsername);

        var stillOther = await resolver.ResolveAsync("authentik", "link-taken-sub-1", null, "taken-owner", null, CancellationToken.None);
        Assert.Equal(other.UserId, stillOther.UserId);
    }

    [Fact]
    public async Task SyncSourceIdentitiesAsync_MultiplePopulatedProviders_LinksEachAsItsOwnRow() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var owner = await resolver.ResolveAsync("authentik", "sync-sub-1", null, "syncer", null, CancellationToken.None);
        var perSourceIds = new Dictionary<string, string?> {
            ["discord"] = "sync-discord-1",
            ["google"] = "sync-google-1",
            ["microsoft"] = null,
            ["github"] = "sync-github-1",
        };

        var results = await resolver.SyncSourceIdentitiesAsync(owner.UserId, perSourceIds, CancellationToken.None);

        Assert.Equal(4, results.Count);
        Assert.True(results.Single(r => r.Provider == "microsoft").Outcome.NotAvailable);
        Assert.All(results.Where(r => r.Provider != "microsoft"), r => Assert.True(r.Outcome.Linked));
        var discordResolved = await resolver.ResolveAsync("discord", "sync-discord-1", null, "syncer", null, CancellationToken.None);
        var githubResolved = await resolver.ResolveAsync("github", "sync-github-1", null, "syncer", null, CancellationToken.None);
        Assert.Equal(owner.UserId, discordResolved.UserId);
        Assert.Equal(owner.UserId, githubResolved.UserId);
    }

    [Fact]
    public async Task SyncSourceIdentitiesAsync_OneProviderClaimedByAnotherUser_ReportsConflictButLinksTheRest() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var otherOwner = await resolver.ResolveAsync("google", "sync-taken-google-1", null, "other", null, CancellationToken.None);
        var owner = await resolver.ResolveAsync("authentik", "sync-sub-2", null, "syncer2", null, CancellationToken.None);
        var perSourceIds = new Dictionary<string, string?> {
            ["discord"] = "sync-discord-2",
            ["google"] = "sync-taken-google-1",
        };

        var results = await resolver.SyncSourceIdentitiesAsync(owner.UserId, perSourceIds, CancellationToken.None);

        var googleOutcome = results.Single(r => r.Provider == "google").Outcome;
        var discordOutcome = results.Single(r => r.Provider == "discord").Outcome;
        Assert.True(googleOutcome.Conflict);
        Assert.True(discordOutcome.Linked);
        var stillOther = await resolver.ResolveAsync("google", "sync-taken-google-1", null, "other", null, CancellationToken.None);
        Assert.Equal(otherOwner.UserId, stillOther.UserId);
    }

    [Fact]
    public async Task SyncSourceIdentitiesAsync_AllNullClaims_ReportsNotAvailable() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);

        var owner = await resolver.ResolveAsync("authentik", "sync-sub-3", null, "syncer3", null, CancellationToken.None);
        var perSourceIds = new Dictionary<string, string?> { ["discord"] = null, ["google"] = null };

        var results = await resolver.SyncSourceIdentitiesAsync(owner.UserId, perSourceIds, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Outcome.NotAvailable));
    }

    [Fact]
    public async Task SyncSourceIdentitiesAsync_RepeatedSync_DoesNotOverwriteOtherIdentitiesUsername() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = MakeResolver(db);
        var profiles = new ProfileService(db);

        var owner = await resolver.ResolveAsync("authentik", "sync-sub-4", null, "first-name", null, CancellationToken.None);
        await resolver.TryLinkAsync(owner.UserId, "discord", "sync-discord-4", "sync-discord-4", "discord-own-name", null, CancellationToken.None);

        await resolver.SyncSourceIdentitiesAsync(
            owner.UserId, new Dictionary<string, string?> { ["google"] = "sync-google-4" }, CancellationToken.None);

        var discordIdentity = (await profiles.ListIdentitiesAsync(owner.UserId, CancellationToken.None))
            .Single(i => i.Provider == "discord");
        Assert.Equal("discord-own-name", discordIdentity.Username);
    }
}
