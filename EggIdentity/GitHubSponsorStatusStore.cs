using EggIdentity.Models;
using Npgsql;

namespace EggIdentity;

public sealed class GitHubSponsorStatusStore(NpgsqlDataSource dataSource) {
    public async Task<GitHubSponsorStatus?> GetAsync(Guid userId, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id, is_sponsor, last_synced_at, updated_at FROM github_sponsor_status WHERE user_id = $1", conn);
        cmd.Parameters.AddWithValue(userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new GitHubSponsorStatus {
            UserId = reader.GetGuid(0),
            IsSponsor = reader.GetBoolean(1),
            LastSyncedAt = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2),
            UpdatedAt = reader.GetFieldValue<DateTimeOffset>(3),
        };
    }

    public async Task UpsertAsync(Guid userId, bool isSponsor, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO github_sponsor_status (user_id, is_sponsor, last_synced_at, updated_at)
            VALUES ($1, $2, now(), now())
            ON CONFLICT (user_id) DO UPDATE SET
                is_sponsor = EXCLUDED.is_sponsor,
                last_synced_at = now(),
                updated_at = now()
            """, conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(isSponsor);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Guid?> FindUserIdByGitHubSubjectAsync(string subject, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id FROM identities WHERE provider = 'github' AND subject = $1", conn);
        cmd.Parameters.AddWithValue(subject);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }
}
