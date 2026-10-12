using EggIdentity.Contract;
using Npgsql;

namespace EggIdentity.Fleet;

public sealed record UpstreamBuild(string Upstream, long Height, string Sha, DateTimeOffset PublishedAt) {
    public string Version => BuildVersion.FromHeight(Height);
}

public interface IUpstreamBuilds {
    Task RecordAsync(string upstream, long height, string sha, CancellationToken ct);
    Task<UpstreamBuild?> LatestAsync(string upstream, CancellationToken ct);
    Task<IReadOnlyList<UpstreamBuild>> ListAsync(string upstream, int limit, CancellationToken ct);
}

public sealed class UpstreamBuildStore(NpgsqlDataSource dataSource) : IUpstreamBuilds {
    public async Task RecordAsync(string upstream, long height, string sha, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO fleet_upstream_builds (upstream, height, sha)
            VALUES ($1, $2, $3)
            ON CONFLICT (upstream, height) DO UPDATE SET sha = $3
            """, conn);
        cmd.Parameters.AddWithValue(upstream);
        cmd.Parameters.AddWithValue(height);
        cmd.Parameters.AddWithValue(sha);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<UpstreamBuild?> LatestAsync(string upstream, CancellationToken ct) {
        var builds = await ListAsync(upstream, 1, ct);
        return builds.Count > 0 ? builds[0] : null;
    }

    public async Task<IReadOnlyList<UpstreamBuild>> ListAsync(string upstream, int limit, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT upstream, height, sha, published_at
            FROM fleet_upstream_builds
            WHERE lower(upstream) = lower($1)
            ORDER BY height DESC
            LIMIT $2
            """, conn);
        cmd.Parameters.AddWithValue(upstream);
        cmd.Parameters.AddWithValue(limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<UpstreamBuild>();
        while (await reader.ReadAsync(ct))
            results.Add(new UpstreamBuild(reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3)));
        return results;
    }
}
