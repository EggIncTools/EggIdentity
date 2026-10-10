using EggIdentity.Contract;
using Npgsql;

namespace EggIdentity.Visits.Tests;

public class VisitsStoreTests {
    private static readonly DateTimeOffset Start = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UpsertAccumulatesAndQuerySummarizes() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrEmpty(conn)) return;

        await using var dataSource = NpgsqlDataSource.Create(conn);
        var store = new VisitsStore(dataSource, new FixedClock(Start));
        await store.MigrateAsync(CancellationToken.None);
        var site = "test-" + Guid.NewGuid().ToString("N");
        var today = new DateOnly(2026, 9, 18);
        var paths = new Dictionary<string, long>(StringComparer.Ordinal) { ["/"] = 2, ["/x"] = 1 };

        try {
            await store.UpsertDayAsync(site, new VisitsDaySnapshot(today, 2, 2, 3, 40, false, paths), CancellationToken.None);
            await store.UpsertDayAsync(site, new VisitsDaySnapshot(today, 1, 0, 1, 5, true, new Dictionary<string, long>(StringComparer.Ordinal) { ["/"] = 1 }), CancellationToken.None);
            await store.UpsertDayAsync(site, new VisitsDaySnapshot(today.AddDays(-1), 1, 1, 1, 1, false, new Dictionary<string, long>(StringComparer.Ordinal)), CancellationToken.None);

            var summary = await store.QueryAsync(site, 7, CancellationToken.None);

            Assert.Equal(site, summary.Site);
            Assert.Equal(7, summary.ByDay.Count);
            Assert.Equal(today, summary.ByDay[^1].Day);
            Assert.Equal(3, summary.ByDay[^1].Visits);
            Assert.Equal(2, summary.ByDay[^1].Visitors);
            Assert.Equal(4, summary.ByDay[^1].Pageviews);
            Assert.Equal(45, summary.ByDay[^1].DurationSeconds);
            Assert.True(summary.ByDay[^1].Capped);
            Assert.Equal(4, summary.Visits);
            Assert.Equal(3, summary.Visitors);
            Assert.True(summary.Capped);
            Assert.Equal(new VisitsPath("/", 3), summary.TopPaths[0]);
            Assert.Equal(new VisitsPath("/x", 1), summary.TopPaths[1]);

            var narrow = await store.QueryAsync(site, 1, CancellationToken.None);
            Assert.Equal(3, narrow.Visits);
        } finally {
            await using var days = dataSource.CreateCommand("DELETE FROM site_visits_daily WHERE site = $1");
            days.Parameters.AddWithValue(site);
            await days.ExecuteNonQueryAsync();
            await using var pathRows = dataSource.CreateCommand("DELETE FROM site_visit_paths_daily WHERE site = $1");
            pathRows.Parameters.AddWithValue(site);
            await pathRows.ExecuteNonQueryAsync();
        }
    }
}
