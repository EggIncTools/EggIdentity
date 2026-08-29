using EggIdentity;
using EggIdentity.Db;
using EggIdentity.Host;
using Npgsql;
using Xunit;

namespace EggIdentity.Host.Tests;

public class SponsorSyncServiceTests {
    private static string? ConnString => Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");

    private sealed class FakeGitHubSponsorClient(bool isSponsor) : IGitHubSponsorClient {
        public Task<bool> IsSponsoredByUserIdAsync(string githubUserId, CancellationToken ct) => Task.FromResult(isSponsor);
    }

    private sealed class RecordingDiscordRoleClient : IDiscordRoleClient {
        public List<(string Action, string GuildId, string UserId, string RoleId)> Calls { get; } = [];

        public Task AddRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct) {
            Calls.Add(("add", guildId, discordUserId, roleId));
            return Task.CompletedTask;
        }

        public Task RemoveRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct) {
            Calls.Add(("remove", guildId, discordUserId, roleId));
            return Task.CompletedTask;
        }

        public Task<bool> HasRoleAsync(string guildId, string discordUserId, string roleId, CancellationToken ct) {
            Calls.Add(("has", guildId, discordUserId, roleId));
            return Task.FromResult(false);
        }
    }

    private static readonly SponsorConfig Config = new(
        GitHubPat: "pat", GitHubTarget: "DavidArthurCole", GitHubWebhookSecret: "whsecret",
        DiscordBotToken: "bottoken", DiscordGuildId: "guild-1", DiscordRoleId: "role-1");

    private static async Task<NpgsqlDataSource> MakeDbAsync() {
        var dataSource = NpgsqlDataSource.Create(ConnString!);
        await using var conn = await dataSource.OpenConnectionAsync();
        await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
        return dataSource;
    }

    private static SponsorSyncService MakeService(
        NpgsqlDataSource db, bool isSponsor, RecordingDiscordRoleClient discord) =>
        new(Config, new FakeGitHubSponsorClient(isSponsor), discord,
            new GitHubSponsorStatusStore(db), new ProfileService(db), new UserQueries(db));

    private static async Task<Guid> MakeUserWithDiscordAsync(NpgsqlDataSource db, string discordSubject) {
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var result = await resolver.ResolveAsync("discord", discordSubject, null, "sync-test-user", null, CancellationToken.None);
        return result.UserId;
    }

    private static async Task LinkGitHubAsync(NpgsqlDataSource db, Guid userId, string githubSubject) {
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        await resolver.TryLinkAsync(userId, "github", githubSubject, null, null, null, CancellationToken.None);
    }

    [Fact]
    public async Task SyncAsync_NoGitHubIdentity_ReturnsNullAndSkipsDiscordCall() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var discord = new RecordingDiscordRoleClient();
        var service = MakeService(db, isSponsor: true, discord);
        var userId = await MakeUserWithDiscordAsync(db, "discord-sync-1");

        var result = await service.SyncAsync(userId, CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(discord.Calls);
    }

    [Fact]
    public async Task SyncAsync_SponsorWithDiscordId_UpsertsStatusAndAddsRole() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var discord = new RecordingDiscordRoleClient();
        var service = MakeService(db, isSponsor: true, discord);
        var userId = await MakeUserWithDiscordAsync(db, "discord-sync-2");
        await LinkGitHubAsync(db, userId, "gh-sync-2");

        var result = await service.SyncAsync(userId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.IsSponsor);
        Assert.Single(discord.Calls);
        Assert.Equal("add", discord.Calls[0].Action);
    }

    [Fact]
    public async Task SyncAsync_NotSponsorWithDiscordId_RemovesRole() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var discord = new RecordingDiscordRoleClient();
        var service = MakeService(db, isSponsor: false, discord);
        var userId = await MakeUserWithDiscordAsync(db, "discord-sync-3");
        await LinkGitHubAsync(db, userId, "gh-sync-3");

        await service.SyncAsync(userId, CancellationToken.None);

        Assert.Single(discord.Calls);
        Assert.Equal("remove", discord.Calls[0].Action);
    }

    [Fact]
    public async Task ApplyWebhookEventAsync_UnknownSubject_NoOp() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var discord = new RecordingDiscordRoleClient();
        var service = MakeService(db, isSponsor: true, discord);

        await service.ApplyWebhookEventAsync("gh-unknown", true, CancellationToken.None);

        Assert.Empty(discord.Calls);
    }

    [Fact]
    public async Task ApplyWebhookEventAsync_KnownSubject_UpsertsAndReconciles() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var discord = new RecordingDiscordRoleClient();
        var service = MakeService(db, isSponsor: false, discord);
        var userId = await MakeUserWithDiscordAsync(db, "discord-sync-4");
        await LinkGitHubAsync(db, userId, "gh-sync-4");

        await service.ApplyWebhookEventAsync("gh-sync-4", true, CancellationToken.None);
        var store = new GitHubSponsorStatusStore(db);
        var status = await store.GetAsync(userId, CancellationToken.None);

        Assert.True(status!.IsSponsor);
        Assert.Single(discord.Calls);
        Assert.Equal("add", discord.Calls[0].Action);
    }

    [Fact]
    public async Task ReconcileRoleAsync_NoStatusRow_TreatsAsNotSponsorAndSkipsDiscordCall() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var discord = new RecordingDiscordRoleClient();
        var service = MakeService(db, isSponsor: true, discord);
        var userId = await MakeUserWithDiscordAsync(db, "discord-sync-5");

        await service.ReconcileRoleAsync(userId, CancellationToken.None);

        Assert.Empty(discord.Calls);
    }
}
