using System.Reflection;
using EggIdentity.Contract;
using EggIdentity.Db;
using Npgsql;

namespace EggIdentity.Visits;

public sealed class VisitsStore(NpgsqlDataSource dataSource, TimeProvider? time = null) : IVisitsSink {
    private const string MigrationsTable = "eggidentity_visits_migrations";
    private const string ResourcePrefix = "EggIdentity.Visits.Migrations.";
    private const int TopPathCount = 50;
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task MigrateAsync(CancellationToken ct = default) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await Migrator.MigrateEmbeddedAsync(conn, Assembly.GetExecutingAssembly(), ResourcePrefix, MigrationsTable, ct);
    }

    public async Task UpsertDayAsync(string site, VisitsDaySnapshot snapshot, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(snapshot);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var day = new NpgsqlCommand(
            """
            INSERT INTO site_visits_daily (site, day, visits, visitors, pageviews, duration_seconds, capped, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, now())
            ON CONFLICT (site, day) DO UPDATE SET
                visits = site_visits_daily.visits + EXCLUDED.visits,
                visitors = site_visits_daily.visitors + EXCLUDED.visitors,
                pageviews = site_visits_daily.pageviews + EXCLUDED.pageviews,
                duration_seconds = site_visits_daily.duration_seconds + EXCLUDED.duration_seconds,
                capped = site_visits_daily.capped OR EXCLUDED.capped,
                updated_at = now()
            """, conn, tx)) {
            day.Parameters.AddWithValue(site);
            day.Parameters.AddWithValue(snapshot.Day);
            day.Parameters.AddWithValue(snapshot.Visits);
            day.Parameters.AddWithValue(snapshot.Visitors);
            day.Parameters.AddWithValue(snapshot.Pageviews);
            day.Parameters.AddWithValue(snapshot.DurationSeconds);
            day.Parameters.AddWithValue(snapshot.Capped);
            await day.ExecuteNonQueryAsync(ct);
        }

        if (snapshot.Paths.Count > 0) {
            await using var batch = new NpgsqlBatch(conn, tx);
            foreach (var (path, views) in snapshot.Paths) {
                var cmd = batch.CreateBatchCommand();
                cmd.CommandText =
                    """
                    INSERT INTO site_visit_paths_daily (site, day, path, pageviews)
                    VALUES ($1, $2, $3, $4)
                    ON CONFLICT (site, day, path) DO UPDATE SET
                        pageviews = site_visit_paths_daily.pageviews + EXCLUDED.pageviews
                    """;
                cmd.Parameters.AddWithValue(site);
                cmd.Parameters.AddWithValue(snapshot.Day);
                cmd.Parameters.AddWithValue(path);
                cmd.Parameters.AddWithValue(views);
                batch.BatchCommands.Add(cmd);
            }
            await batch.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task<VisitsSummary> QueryAsync(string site, int days, CancellationToken ct = default) {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        var since = today.AddDays(1 - days);
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var rows = new Dictionary<DateOnly, VisitsDay>();
        await using (var cmd = new NpgsqlCommand(
            """
            SELECT day, visits, visitors, pageviews, duration_seconds, capped
            FROM site_visits_daily
            WHERE site = $1 AND day >= $2
            """, conn)) {
            cmd.Parameters.AddWithValue(site);
            cmd.Parameters.AddWithValue(since);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) {
                var day = reader.GetFieldValue<DateOnly>(0);
                rows[day] = new VisitsDay(
                    day, reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetBoolean(5));
            }
        }

        var byDay = new List<VisitsDay>(days);
        for (var d = since; d <= today; d = d.AddDays(1)) {
            byDay.Add(rows.TryGetValue(d, out var row) ? row : new VisitsDay(d, 0, 0, 0, 0, false));
        }

        var paths = new List<VisitsPath>();
        await using (var cmd = new NpgsqlCommand(
            """
            SELECT path, SUM(pageviews) AS pageviews
            FROM site_visit_paths_daily
            WHERE site = $1 AND day >= $2
            GROUP BY path
            ORDER BY pageviews DESC, path
            LIMIT $3
            """, conn)) {
            cmd.Parameters.AddWithValue(site);
            cmd.Parameters.AddWithValue(since);
            cmd.Parameters.AddWithValue(TopPathCount);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) paths.Add(new VisitsPath(reader.GetString(0), reader.GetInt64(1)));
        }

        return new VisitsSummary(
            site,
            days,
            byDay.Sum(d => d.Visitors),
            byDay.Sum(d => d.Visits),
            byDay.Sum(d => d.Pageviews),
            byDay.Sum(d => d.DurationSeconds),
            byDay.Exists(d => d.Capped),
            byDay,
            paths);
    }
}
