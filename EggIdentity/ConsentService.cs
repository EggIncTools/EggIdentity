using EggIdentity.Models;
using Npgsql;

namespace EggIdentity;

public sealed class ConsentService(NpgsqlDataSource dataSource) {
    public async Task<CookieConsent?> GetAsync(Guid userId, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id, functional, analytics, policy_version, decided_at, updated_at FROM cookie_consent WHERE user_id = $1",
            conn);
        cmd.Parameters.AddWithValue(userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return !await reader.ReadAsync(ct) ? null : new CookieConsent {
            UserId = reader.GetGuid(0),
            Functional = reader.GetBoolean(1),
            Analytics = reader.GetBoolean(2),
            PolicyVersion = reader.GetInt32(3),
            DecidedAt = reader.GetFieldValue<DateTimeOffset>(4),
            UpdatedAt = reader.GetFieldValue<DateTimeOffset>(5),
        };
    }

    public async Task SetAsync(Guid userId, bool functional, bool analytics, int policyVersion, DateTimeOffset decidedAt, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO cookie_consent (user_id, functional, analytics, policy_version, decided_at)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (user_id) DO UPDATE SET
                functional = EXCLUDED.functional,
                analytics = EXCLUDED.analytics,
                policy_version = EXCLUDED.policy_version,
                decided_at = EXCLUDED.decided_at,
                updated_at = now()
            """, conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(functional);
        cmd.Parameters.AddWithValue(analytics);
        cmd.Parameters.AddWithValue(policyVersion);
        cmd.Parameters.AddWithValue(decidedAt.ToUniversalTime());
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
