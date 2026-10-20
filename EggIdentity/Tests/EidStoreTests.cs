using EggIdentity.Contract;
using EggIdentity.Db;
using Npgsql;

namespace EggIdentity.Tests;

public class EidStoreTests {
    private static string? ConnString => Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");

    private static async Task<NpgsqlDataSource> MakeDbAsync() {
        var dataSource = NpgsqlDataSource.Create(ConnString!);
        await using var conn = await dataSource.OpenConnectionAsync();
        await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
        return dataSource;
    }

    private static string Eid(int n) => "EI" + n.ToString("D16", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<Guid> NewUserAsync(NpgsqlDataSource db, string subject) {
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var resolved = await resolver.ResolveAsync("discord", subject + Guid.NewGuid().ToString("N"), null, subject, null, CancellationToken.None);
        return resolved.UserId;
    }

    private static List<string> Ids(IReadOnlyList<ProfileEid> list) => [.. list.Select(e => e.Eid)];

    [Fact]
    public async Task UpsertAppendsInBodyOrderAndKeepsExistingPositions() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new EidStore(db);
        var user = await NewUserAsync(db, "eid-order");

        await store.UpsertAsync(user, [new EidUpsert { Eid = Eid(1) }, new EidUpsert { Eid = Eid(2) }], CancellationToken.None);
        var result = await store.UpsertAsync(user, [new EidUpsert { Eid = Eid(3) }, new EidUpsert { Eid = Eid(1), Label = "ignored" }], CancellationToken.None);

        Assert.Equal([Eid(1), Eid(2), Eid(3)], Ids(result.Eids));
        Assert.Null(result.Eids[0].Label);
    }

    [Fact]
    public async Task UpsertAcceptsValidEntriesAndListsRejections() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new EidStore(db);
        var user = await NewUserAsync(db, "eid-partial");

        var result = await store.UpsertAsync(user, [
            new EidUpsert { Eid = "bogus" },
            new EidUpsert { Eid = Eid(1), Label = new string('x', EidRules.MaxLabelLength + 1) },
            new EidUpsert { Eid = Eid(2) },
        ], CancellationToken.None);

        Assert.Equal([Eid(2)], Ids(result.Eids));
        Assert.Equal([EidRules.FormatReason, EidRules.LabelReason], result.Rejected.Select(r => r.Reason));
    }

    [Fact]
    public async Task UpsertRejectsEntriesPastTheCap() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new EidStore(db);
        var user = await NewUserAsync(db, "eid-cap");

        var entries = Enumerable.Range(1, EidRules.MaxEntries + 2).Select(n => new EidUpsert { Eid = Eid(n) }).ToList();
        var result = await store.UpsertAsync(user, entries, CancellationToken.None);

        Assert.Equal(EidRules.MaxEntries, result.Eids.Count);
        Assert.All(result.Rejected, r => Assert.Equal(EidRules.CapReason, r.Reason));
        Assert.Equal(2, result.Rejected.Count);
    }

    [Fact]
    public async Task SnapshotUpdateNeverInserts() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new EidStore(db);
        var user = await NewUserAsync(db, "eid-snapshot");
        await store.UpsertAsync(user, [new EidUpsert { Eid = Eid(1) }], CancellationToken.None);

        Assert.True(await store.UpdateSnapshotAsync(user, Eid(1), new EidSnapshot { GameName = "Farmer" }, CancellationToken.None));
        Assert.False(await store.UpdateSnapshotAsync(user, Eid(2), new EidSnapshot { GameName = "Ghost" }, CancellationToken.None));

        var list = await store.ListAsync(user, CancellationToken.None);
        Assert.Equal([Eid(1)], Ids(list));
        Assert.Equal("Farmer", list[0].DisplayName);
        Assert.NotNull(list[0].SnapshotAt);
    }

    [Fact]
    public async Task RemoveCompactsPositionsAndClearsActive() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new EidStore(db);
        var profiles = new ProfileService(db);
        var users = new UserQueries(db);
        var user = await NewUserAsync(db, "eid-remove");
        await store.UpsertAsync(user, [new EidUpsert { Eid = Eid(1) }, new EidUpsert { Eid = Eid(2) }, new EidUpsert { Eid = Eid(3) }], CancellationToken.None);
        await profiles.SetGameAsync(user, Eid(2), null, CancellationToken.None);

        Assert.True(await store.RemoveAsync(user, Eid(2), CancellationToken.None));
        await store.UpsertAsync(user, [new EidUpsert { Eid = Eid(4) }], CancellationToken.None);

        Assert.Equal([Eid(1), Eid(3), Eid(4)], Ids(await store.ListAsync(user, CancellationToken.None)));
        Assert.Null((await users.GetAsync(user, CancellationToken.None))!.ActiveEid);
    }

    [Fact]
    public async Task ReorderRequiresAPermutation() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new EidStore(db);
        var user = await NewUserAsync(db, "eid-reorder");
        await store.UpsertAsync(user, [new EidUpsert { Eid = Eid(1) }, new EidUpsert { Eid = Eid(2) }], CancellationToken.None);

        Assert.Equal(EidReorderResult.NotPermutation, await store.ReorderAsync(user, [Eid(1)], CancellationToken.None));
        Assert.Equal(EidReorderResult.Reordered, await store.ReorderAsync(user, [Eid(2), Eid(1)], CancellationToken.None));
        Assert.Equal([Eid(2), Eid(1)], Ids(await store.ListAsync(user, CancellationToken.None)));
    }

    [Fact]
    public async Task ConcurrentInsertsKeepPositionsDense() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new EidStore(db);
        var user = await NewUserAsync(db, "eid-race");

        await Task.WhenAll(Enumerable.Range(1, 10).Select(n => store.UpsertAsync(user, [new EidUpsert { Eid = Eid(n) }], CancellationToken.None)));

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT position FROM user_eids WHERE user_id = $1 ORDER BY position", conn);
        cmd.Parameters.AddWithValue(user);
        var positions = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) positions.Add(reader.GetInt32(0));
        Assert.Equal(Enumerable.Range(0, 10), positions);
    }

    [Fact]
    public async Task MergeAppendsMissingEntriesUpToTheCapAndCarriesActive() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var store = new EidStore(db);
        var profiles = new ProfileService(db);
        var users = new UserQueries(db);
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var keep = await NewUserAsync(db, "eid-keep");
        var merge = await NewUserAsync(db, "eid-merge");

        var keptEntries = Enumerable.Range(1, EidRules.MaxEntries - 1).Select(n => new EidUpsert { Eid = Eid(n) }).ToList();
        await store.UpsertAsync(keep, keptEntries, CancellationToken.None);
        await store.UpsertAsync(merge, [new EidUpsert { Eid = Eid(1) }, new EidUpsert { Eid = Eid(900) }, new EidUpsert { Eid = Eid(901) }], CancellationToken.None);
        await profiles.SetGameAsync(merge, Eid(900), null, CancellationToken.None);

        await resolver.MergeAsync(keep, merge, CancellationToken.None);

        var list = Ids(await store.ListAsync(keep, CancellationToken.None));
        Assert.Equal(EidRules.MaxEntries, list.Count);
        Assert.Equal(Eid(900), list[^1]);
        Assert.DoesNotContain(Eid(901), list);
        Assert.Equal(Eid(900), (await users.GetAsync(keep, CancellationToken.None))!.ActiveEid);
    }
}
