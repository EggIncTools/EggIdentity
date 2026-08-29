using EggIdentity;
using EggIdentity.Db;
using EggIdentity.Models;
using Npgsql;

namespace EggIdentity.Tests;

public class GitHubSponsorStatusStoreTests {
    private static string? ConnString => Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");

    private static async Task<NpgsqlDataSource> MakeDbAsync() {
        var dataSource = NpgsqlDataSource.Create(ConnString!);
        await using var conn = await dataSource.OpenConnectionAsync();
        await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
        return dataSource;
    }

    private static async Task<Guid> MakeUserAsync(NpgsqlDataSource db, string discordSubject) {
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var result = await resolver.ResolveAsync("discord", discordSubject, null, "sponsor-test-user", null, CancellationToken.None);
        return result.UserId;
    }

    [Fact]
    public async Task GetAsync_NoRow_ReturnsNull() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new GitHubSponsorStatusStore(db);

        var result = await store.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertAsync_NewUser_InsertsRow() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new GitHubSponsorStatusStore(db);
        var userId = await MakeUserAsync(db, "discord-sponsor-1");

        await store.UpsertAsync(userId, true, CancellationToken.None);
        var result = await store.GetAsync(userId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSponsor);
        Assert.NotNull(result.LastSyncedAt);
    }

    [Fact]
    public async Task UpsertAsync_ExistingRow_UpdatesInPlace() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new GitHubSponsorStatusStore(db);
        var userId = await MakeUserAsync(db, "discord-sponsor-2");

        await store.UpsertAsync(userId, true, CancellationToken.None);
        await store.UpsertAsync(userId, false, CancellationToken.None);
        var result = await store.GetAsync(userId, CancellationToken.None);

        Assert.False(result!.IsSponsor);
    }

    [Fact]
    public async Task FindUserIdByGitHubSubjectAsync_LinkedIdentity_ReturnsUserId() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new GitHubSponsorStatusStore(db);
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var userId = await MakeUserAsync(db, "discord-sponsor-3");
        await resolver.TryLinkAsync(userId, "github", "gh-12345", null, null, null, CancellationToken.None);

        var found = await store.FindUserIdByGitHubSubjectAsync("gh-12345", CancellationToken.None);

        Assert.Equal(userId, found);
    }

    [Fact]
    public async Task FindUserIdByGitHubSubjectAsync_UnknownSubject_ReturnsNull() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new GitHubSponsorStatusStore(db);

        var found = await store.FindUserIdByGitHubSubjectAsync("gh-nonexistent", CancellationToken.None);

        Assert.Null(found);
    }
}
