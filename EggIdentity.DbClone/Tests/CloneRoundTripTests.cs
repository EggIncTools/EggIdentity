using System.Globalization;
using Npgsql;

namespace EggIdentity.DbClone.Tests;

public class CloneRoundTripTests {
    private static string? AdminConn => Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");

    private const string Schema =
        "CREATE TABLE parent (id INT PRIMARY KEY, name TEXT NOT NULL, active BOOL NOT NULL DEFAULT true);"
        + "CREATE TABLE child (id INT PRIMARY KEY, parent_id INT NOT NULL REFERENCES parent(id), secret TEXT NOT NULL, owner UUID);"
        + "CREATE TABLE cache (id INT PRIMARY KEY, blob TEXT);"
        + "CREATE TABLE app_settings (key TEXT PRIMARY KEY, value TEXT);"
        + "CREATE TABLE ledger (version INT PRIMARY KEY);";

    private const string Seed =
        "INSERT INTO parent VALUES (1, 'one', true), (2, 'two', false), (3, 'three', true);"
        + "INSERT INTO child VALUES (10, 1, 'hunter2', '11111111-1111-1111-1111-111111111111'), (30, 3, 'p4ss', '22222222-2222-2222-2222-222222222222');"
        + "INSERT INTO cache VALUES (1, 'x');"
        + "INSERT INTO app_settings VALUES ('prod.key', 'prod');";

    [Fact]
    public async Task Clone_CopiesScrubsTruncatesStampsAndVerifies() {
        if (string.IsNullOrEmpty(AdminConn)) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var sourceDb = $"eggidentity_clone_src_{suffix}";
        var targetDb = $"eggidentity_clone_tgt_{suffix}";
        await using var admin = new NpgsqlConnection(AdminConn);
        await admin.OpenAsync();
        await ExecAsync(admin, $"CREATE DATABASE {SqlIdentifier.Quote(sourceDb)}");
        await ExecAsync(admin, $"CREATE DATABASE {SqlIdentifier.Quote(targetDb)}");
        try {
            var sourceConn = ConnTo(sourceDb);
            var targetConn = ConnTo(targetDb);
            await PrepareAsync(sourceConn, Schema + Seed);
            await PrepareAsync(targetConn, Schema + "INSERT INTO app_settings VALUES ('sub.key', 'sub'); INSERT INTO cache VALUES (9, 'stale');");

            var plan = new ClonePlan("app", targetDb, [
                new TablePolicy("parent", ClonePolicy.Full) { Where = "active" },
                new TablePolicy("child", ClonePolicy.Scrub) {
                    ScrubSql = "UPDATE child SET secret = ''",
                    VerifySql = "SELECT 1 FROM child WHERE secret <> ''",
                    UserIdColumns = ["owner"],
                },
                new TablePolicy("cache", ClonePolicy.SchemaOnly),
                new TablePolicy("app_settings", ClonePolicy.Skip),
            ]) { IgnoredTables = ["ledger"] };

            var events = new List<CloneEvent>();
            var progress = new ListProgress(events);
            var dry = await CloneRunner.RunAsync(plan, sourceConn, targetConn, true, progress, CancellationToken.None);
            Assert.Equal(1, await CountAsync(targetConn, "cache"));
            Assert.Equal(2, dry.Single(c => c.Table == "parent").SourceRows);

            var counts = await CloneRunner.RunAsync(plan, sourceConn, targetConn, false, progress, CancellationToken.None);

            Assert.Equal(2, counts.Single(c => c.Table == "parent").TargetRows);
            Assert.Equal(2, await CountAsync(targetConn, "child"));
            Assert.Equal(0, await CountAsync(targetConn, "cache"));
            Assert.Equal(1, await CountAsync(targetConn, "app_settings"));
            Assert.Equal(3, await CountAsync(sourceConn, "parent"));
            Assert.Equal("hunter2", await ScalarAsync(sourceConn, "SELECT secret FROM child WHERE id = 10"));
            Assert.Equal(1, await CountAsync(targetConn, PlanValidator.StampTable));
            Assert.Contains(events, e => e.Phase == ClonePhase.Done);

            var stamp = await CloneRunner.LastStampAsync(targetConn, CancellationToken.None);
            Assert.Equal(sourceDb, stamp?.SourceDatabase);

            var results = await CloneVerifier.VerifyAsync(
                plan, sourceConn, targetConn, ["11111111-1111-1111-1111-111111111111"], CancellationToken.None);
            Assert.All(results.Where(r => !r.Detail.StartsWith("owner", StringComparison.Ordinal)), r => Assert.True(r.Ok, r.Detail));
            var orphans = results.Single(r => r.Detail.StartsWith("owner", StringComparison.Ordinal));
            Assert.False(orphans.Ok);
            Assert.Contains("1 orphan", orphans.Detail, StringComparison.Ordinal);
        } finally {
            await ExecAsync(admin, $"DROP DATABASE {SqlIdentifier.Quote(sourceDb)} WITH (FORCE)");
            await ExecAsync(admin, $"DROP DATABASE {SqlIdentifier.Quote(targetDb)} WITH (FORCE)");
        }
    }

    [Fact]
    public async Task Clone_RejectsUncoveredTargetTableBeforeWriting() {
        if (string.IsNullOrEmpty(AdminConn)) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var sourceDb = $"eggidentity_clone_src_{suffix}";
        var targetDb = $"eggidentity_clone_tgt_{suffix}";
        await using var admin = new NpgsqlConnection(AdminConn);
        await admin.OpenAsync();
        await ExecAsync(admin, $"CREATE DATABASE {SqlIdentifier.Quote(sourceDb)}");
        await ExecAsync(admin, $"CREATE DATABASE {SqlIdentifier.Quote(targetDb)}");
        try {
            var sourceConn = ConnTo(sourceDb);
            var targetConn = ConnTo(targetDb);
            await PrepareAsync(sourceConn, Schema + Seed);
            await PrepareAsync(targetConn, Schema + "INSERT INTO cache VALUES (9, 'stale');");
            var plan = new ClonePlan("app", targetDb, [new TablePolicy("parent", ClonePolicy.Full)]);

            var e = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CloneRunner.RunAsync(plan, sourceConn, targetConn, false, new ListProgress([]), CancellationToken.None));

            Assert.Contains("child exists on the target but has no policy", e.Message, StringComparison.Ordinal);
            Assert.Equal(1, await CountAsync(targetConn, "cache"));
        } finally {
            await ExecAsync(admin, $"DROP DATABASE {SqlIdentifier.Quote(sourceDb)} WITH (FORCE)");
            await ExecAsync(admin, $"DROP DATABASE {SqlIdentifier.Quote(targetDb)} WITH (FORCE)");
        }
    }

    private static string ConnTo(string database) =>
        new NpgsqlConnectionStringBuilder(AdminConn!) { Database = database, Pooling = false }.ConnectionString;

    private static async Task PrepareAsync(string connString, string sql) {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await ExecAsync(conn, sql);
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql) {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(string connString, string table) =>
        Convert.ToInt64(await ScalarAsync(connString, $"SELECT count(*) FROM {SqlIdentifier.Quote(table)}"), CultureInfo.InvariantCulture);

    private static async Task<object?> ScalarAsync(string connString, string sql) {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return await cmd.ExecuteScalarAsync();
    }

    private sealed class ListProgress(List<CloneEvent> sink) : IProgress<CloneEvent> {
        public void Report(CloneEvent value) => sink.Add(value);
    }
}
