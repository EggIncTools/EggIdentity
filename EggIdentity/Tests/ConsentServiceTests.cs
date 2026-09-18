using EggIdentity.Db;
using Npgsql;

namespace EggIdentity.Tests;

public class ConsentServiceTests {
    private static string? ConnString => Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");

    private static async Task<NpgsqlDataSource> MakeDbAsync() {
        var dataSource = NpgsqlDataSource.Create(ConnString!);
        await using var conn = await dataSource.OpenConnectionAsync();
        await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
        return dataSource;
    }

    [Fact]
    public async Task GetAsync_NoRow_ReturnsNull() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var svc = new ConsentService(db);

        var owner = await resolver.ResolveAsync("discord", "cs-owner-1", null, "csowner1", null, CancellationToken.None);

        Assert.Null(await svc.GetAsync(owner.UserId, CancellationToken.None));
    }

    [Fact]
    public async Task SetAsync_ThenGet_RoundTrips() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var svc = new ConsentService(db);

        var owner = await resolver.ResolveAsync("discord", "cs-owner-2", null, "csowner2", null, CancellationToken.None);
        var decidedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        await svc.SetAsync(owner.UserId, true, false, 1, decidedAt, CancellationToken.None);

        var row = await svc.GetAsync(owner.UserId, CancellationToken.None);

        Assert.NotNull(row);
        Assert.True(row.Functional);
        Assert.False(row.Analytics);
        Assert.Equal(1, row.PolicyVersion);
        Assert.Equal(decidedAt, row.DecidedAt);
    }

    [Fact]
    public async Task SetAsync_Twice_Upserts() {
        if (string.IsNullOrEmpty(ConnString)) return;
        await using var db = await MakeDbAsync();
        var resolver = new IdentityResolver(db, AdminAllowlist.FromConfig(""));
        var svc = new ConsentService(db);

        var owner = await resolver.ResolveAsync("discord", "cs-owner-3", null, "csowner3", null, CancellationToken.None);
        await svc.SetAsync(owner.UserId, false, false, 1, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), CancellationToken.None);
        await svc.SetAsync(owner.UserId, true, true, 2, DateTimeOffset.FromUnixTimeSeconds(1_700_000_100), CancellationToken.None);

        var row = await svc.GetAsync(owner.UserId, CancellationToken.None);

        Assert.NotNull(row);
        Assert.True(row.Functional);
        Assert.True(row.Analytics);
        Assert.Equal(2, row.PolicyVersion);
    }
}
