using EggIdentity.Contract;
using EggIdentity.Models;
using Npgsql;

namespace EggIdentity;

public sealed class UserQueries(NpgsqlDataSource dataSource) {
    public async Task<User?> GetAsync(Guid userId, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id, discord_id, username, avatar, role, created_at, last_login_at, avatar_is_custom, timezone, language, theme FROM users WHERE user_id = $1",
            conn);
        cmd.Parameters.AddWithValue(userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id, discord_id, username, avatar, role, created_at, last_login_at, avatar_is_custom, timezone, language, theme FROM users ORDER BY created_at",
            conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<User>();
        while (await reader.ReadAsync(ct)) results.Add(Read(reader));
        return results;
    }

    public async Task<IReadOnlyDictionary<Guid, List<string>>> ListProvidersAsync(CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id, provider FROM identities WHERE provider <> 'authentik' ORDER BY user_id, provider",
            conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var result = new Dictionary<Guid, List<string>>();
        while (await reader.ReadAsync(ct)) {
            var userId = reader.GetGuid(0);
            var provider = reader.GetString(1);
            if (!result.TryGetValue(userId, out var list)) {
                list = [];
                result[userId] = list;
            }

            if (!list.Contains(provider)) list.Add(provider);
        }

        return result;
    }

    public async Task<IReadOnlyList<UserMerge>> ListMergesAsync(DateTimeOffset? since, int limit, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT merged_user_id, kept_user_id, merged_at FROM user_merges
            WHERE $1::timestamptz IS NULL OR merged_at > $1
            ORDER BY merged_at, merged_user_id
            LIMIT $2
            """, conn);
        cmd.Parameters.AddWithValue((object?)since ?? DBNull.Value);
        cmd.Parameters.AddWithValue(limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<UserMerge>();
        while (await reader.ReadAsync(ct)) {
            results.Add(new UserMerge {
                MergedUserId = reader.GetGuid(0),
                KeptUserId = reader.GetGuid(1),
                MergedAt = reader.GetFieldValue<DateTimeOffset>(2),
            });
        }
        return results;
    }

    public async Task<IReadOnlyList<Identity>> ListAuthentikIdentitiesAsync(CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT i.user_id, i.provider, i.subject, i.username, i.avatar, i.linked_at
            FROM identities i JOIN users u ON u.user_id = i.user_id
            WHERE i.provider = 'authentik' AND i.username IS NOT NULL
            ORDER BY u.created_at, i.user_id
            """, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<Identity>();
        while (await reader.ReadAsync(ct)) {
            results.Add(new Identity {
                UserId = reader.GetGuid(0),
                Provider = reader.GetString(1),
                Subject = reader.GetString(2),
                Username = reader.IsDBNull(3) ? null : reader.GetString(3),
                Avatar = reader.IsDBNull(4) ? null : reader.GetString(4),
                LinkedAt = reader.GetFieldValue<DateTimeOffset>(5),
            });
        }
        return results;
    }

    public async Task<bool> SetRoleAsync(Guid userId, UserRole role, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE users SET role = $2 WHERE user_id = $1", conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(UserRoles.ToName(role));
        var affected = await cmd.ExecuteNonQueryAsync(ct);
        return affected > 0;
    }

    private static User Read(NpgsqlDataReader reader) => new() {
        UserId = reader.GetGuid(0),
        DiscordId = reader.IsDBNull(1) ? null : reader.GetString(1),
        Username = reader.GetString(2),
        Avatar = reader.IsDBNull(3) ? null : reader.GetString(3),
        Role = reader.GetString(4),
        CreatedAt = reader.GetFieldValue<DateTimeOffset>(5),
        LastLoginAt = reader.GetFieldValue<DateTimeOffset>(6),
        AvatarIsCustom = reader.GetBoolean(7),
        Timezone = reader.IsDBNull(8) ? null : reader.GetString(8),
        Language = reader.IsDBNull(9) ? null : reader.GetString(9),
        Theme = reader.IsDBNull(10) ? null : reader.GetString(10),
    };
}
