using EggIdentity.Db;
using Npgsql;

namespace EggIdentity.Host.Tests;

public class IdentityReconcilerTests {
    private static string? ConnString => Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");

    private static EggIdentity.Models.Identity Local(string provider, string subject) => new() {
        UserId = Guid.Empty,
        Provider = provider,
        Subject = subject,
        LinkedAt = DateTimeOffset.UtcNow,
    };

    private static AuthentikSourceConnection Remote(long pk, string slug, string identifier) =>
        new(pk, slug, slug, identifier);

    [Fact]
    public void ComputeDiff_RemoteHasMore_AddsMissingOnly() {
        var local = new[] { Local("authentik", "sub"), Local("discord", "d-1") };
        var remote = new[] { Remote(1, "discord", "d-1"), Remote(2, "google", "g-1"), Remote(3, "microsoft", "m-1") };

        var diff = IdentityReconciler.ComputeDiff(local, remote);

        Assert.Equal(["google", "microsoft"], diff.ToAdd.Select(c => c.Provider));
        Assert.Empty(diff.ToRemove);
    }

    [Fact]
    public void ComputeDiff_LocalHasStaleRow_RemovesIt() {
        var local = new[] { Local("authentik", "sub"), Local("discord", "d-1"), Local("github", "gh-old") };
        var remote = new[] { Remote(1, "discord", "d-1") };

        var diff = IdentityReconciler.ComputeDiff(local, remote);

        Assert.Empty(diff.ToAdd);
        var removed = Assert.Single(diff.ToRemove);
        Assert.Equal(("github", "gh-old"), (removed.Provider, removed.Subject));
    }

    [Fact]
    public void ComputeDiff_SubjectChangedForProvider_RemovesOldAddsNew() {
        var local = new[] { Local("authentik", "sub"), Local("google", "g-old") };
        var remote = new[] { Remote(5, "google", "g-new") };

        var diff = IdentityReconciler.ComputeDiff(local, remote);

        Assert.Equal("g-new", Assert.Single(diff.ToAdd).Identifier);
        Assert.Equal("g-old", Assert.Single(diff.ToRemove).Subject);
    }

    [Fact]
    public void ComputeDiff_NeverTouchesAuthentikRowOrUnknownSources() {
        var local = new[] { Local("authentik", "sub") };
        var remote = new[] { Remote(1, "corp-ldap", "cn=x") };

        var diff = IdentityReconciler.ComputeDiff(local, remote);

        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.ToRemove);
    }

    [Fact]
    public void ComputeDiff_DuplicateProviderConnections_KeepsOldest() {
        var remote = new[] { Remote(9, "discord", "d-newer"), Remote(2, "discord", "d-older") };

        var diff = IdentityReconciler.ComputeDiff([], remote);

        Assert.Equal("d-older", Assert.Single(diff.ToAdd).Identifier);
    }

    private sealed class FakeAuthentik(IReadOnlyList<AuthentikSourceConnection> connections) : IAuthentikAdminClient {
        public List<long> Deleted { get; } = [];

        public Task<string?> FindUserPkAsync(string username, string subject, CancellationToken ct) =>
            Task.FromResult<string?>(username == "missing" ? null : "7");

        public Task<IReadOnlyList<AuthentikSourceConnection>> ListConnectionsAsync(string userPk, CancellationToken ct) =>
            Task.FromResult(connections);

        public Task DeleteConnectionAsync(long connectionPk, CancellationToken ct) {
            Deleted.Add(connectionPk);
            return Task.CompletedTask;
        }
    }

    private static async Task<NpgsqlDataSource> MakeDbAsync() {
        var dataSource = NpgsqlDataSource.Create(ConnString!);
        await using var conn = await dataSource.OpenConnectionAsync();
        await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
        return dataSource;
    }

    [Fact]
    public async Task ReconcileAsync_MirrorsAuthentikConnectionsIntoIdentities() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var profiles = new ProfileService(db);
        var owner = await resolver.ResolveAsync("authentik", "rc-sub-1", null, "rc-user-1", null, CancellationToken.None);
        await resolver.TryLinkAsync(owner.UserId, "github", "rc-gh-stale", null, null, null, CancellationToken.None);
        var authentik = new FakeAuthentik([
            new AuthentikSourceConnection(1, "discord", "Discord", "rc-d-1"),
            new AuthentikSourceConnection(2, "google", "Google", "rc-g-1"),
        ]);
        var reconciler = new IdentityReconciler(authentik, resolver, profiles);

        var result = await reconciler.ReconcileAsync(owner.UserId, "rc-user-1", "rc-sub-1", CancellationToken.None);

        Assert.True(result.Applied);
        var providers = (await profiles.ListIdentitiesAsync(owner.UserId, CancellationToken.None)).Select(i => i.Provider).Order().ToList();
        Assert.Equal(["authentik", "discord", "google"], providers);
        Assert.Equal("github", Assert.Single(result.Removed).Provider);
    }

    [Fact]
    public async Task ReconcileAsync_UnknownAuthentikUser_LeavesRowsAlone() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var profiles = new ProfileService(db);
        var owner = await resolver.ResolveAsync("authentik", "rc-sub-2", null, "missing", null, CancellationToken.None);
        await resolver.TryLinkAsync(owner.UserId, "github", "rc-gh-2", null, null, null, CancellationToken.None);
        var reconciler = new IdentityReconciler(new FakeAuthentik([]), resolver, profiles);

        var result = await reconciler.ReconcileAsync(owner.UserId, CancellationToken.None);

        Assert.False(result.Applied);
        Assert.Equal(2, (await profiles.ListIdentitiesAsync(owner.UserId, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task UnlinkAsync_DeletesMatchingAuthentikConnection() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var profiles = new ProfileService(db);
        var owner = await resolver.ResolveAsync("authentik", "rc-sub-3", null, "rc-user-3", null, CancellationToken.None);
        var authentik = new FakeAuthentik([new AuthentikSourceConnection(77, "github", "GitHub", "rc-gh-3")]);
        var reconciler = new IdentityReconciler(authentik, resolver, profiles);

        var ok = await reconciler.UnlinkAsync(owner.UserId, "github", "rc-gh-3", CancellationToken.None);

        Assert.True(ok);
        Assert.Equal([77L], authentik.Deleted);
    }
}
