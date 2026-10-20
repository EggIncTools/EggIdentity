using System.Text.Json;
using EggIdentity.Contract;
using Npgsql;
using NpgsqlTypes;

namespace EggIdentity;

public enum EidReorderResult { Reordered, NotPermutation }

public sealed class EidStore(NpgsqlDataSource dataSource) {
    public async Task<IReadOnlyList<ProfileEid>> ListAsync(Guid userId, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await ListAsync(conn, userId, ct);
    }

    public async Task<EidUpsertResult> UpsertAsync(Guid userId, IReadOnlyList<EidUpsert> entries, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(entries);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await LockUserAsync(conn, userId, ct);

        var existing = (await ListAsync(conn, userId, ct)).Select(e => e.Eid).ToHashSet(StringComparer.Ordinal);
        var rejected = new List<EidRejection>();
        foreach (var entry in entries) {
            var eid = entry.Eid?.Trim() ?? "";
            var label = EidRules.NormalizeLabel(entry.Label);
            var reason = Reject(eid, label, existing);
            if (reason is not null) {
                rejected.Add(new EidRejection { Eid = eid, Reason = reason });
                continue;
            }
            if (existing.Contains(eid)) {
                if (entry.Snapshot is not null) await WriteSnapshotAsync(conn, userId, eid, entry.Snapshot, ct);
                continue;
            }
            await InsertAsync(conn, userId, eid, label, entry.Snapshot, existing.Count, ct);
            existing.Add(eid);
        }

        var list = await ListAsync(conn, userId, ct);
        await tx.CommitAsync(ct);
        return new EidUpsertResult { Eids = list, Rejected = rejected };
    }

    public async Task<bool> EnsureAsync(Guid userId, string eid, string? label, CancellationToken ct) {
        var result = await UpsertAsync(userId, [new EidUpsert { Eid = eid, Label = label }], ct);
        return result.Rejected.Count == 0;
    }

    public async Task<bool> UpdateSnapshotAsync(Guid userId, string eid, EidSnapshot snapshot, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(snapshot);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        return await WriteSnapshotAsync(conn, userId, eid, snapshot, ct);
    }

    public async Task<bool> RenameAsync(Guid userId, string eid, string? label, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("UPDATE user_eids SET label = $3 WHERE user_id = $1 AND eid = $2", conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(eid);
        cmd.Parameters.AddWithValue((object?)EidRules.NormalizeLabel(label) ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> RemoveAsync(Guid userId, string eid, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await LockUserAsync(conn, userId, ct);

        int removed;
        await using (var cmd = new NpgsqlCommand("DELETE FROM user_eids WHERE user_id = $1 AND eid = $2", conn)) {
            cmd.Parameters.AddWithValue(userId);
            cmd.Parameters.AddWithValue(eid);
            removed = await cmd.ExecuteNonQueryAsync(ct);
        }
        if (removed == 0) {
            await tx.CommitAsync(ct);
            return false;
        }

        await using (var cmd = new NpgsqlCommand(
            "UPDATE users SET active_eid = NULL, active_eid_label = NULL WHERE user_id = $1 AND active_eid = $2", conn)) {
            cmd.Parameters.AddWithValue(userId);
            cmd.Parameters.AddWithValue(eid);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await CompactAsync(conn, userId, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<EidReorderResult> ReorderAsync(Guid userId, IReadOnlyList<string> eids, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(eids);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await LockUserAsync(conn, userId, ct);

        var current = (await ListAsync(conn, userId, ct)).Select(e => e.Eid).ToList();
        var permutation = eids.Count == current.Count
            && eids.Distinct(StringComparer.Ordinal).Count() == eids.Count
            && eids.All(current.Contains);
        if (!permutation) {
            await tx.CommitAsync(ct);
            return EidReorderResult.NotPermutation;
        }

        for (var i = 0; i < eids.Count; i++) {
            await SetPositionAsync(conn, userId, eids[i], i, ct);
        }
        await tx.CommitAsync(ct);
        return EidReorderResult.Reordered;
    }

    public static async Task<IReadOnlyList<string>> MergeAsync(NpgsqlConnection conn, Guid keepUserId, Guid mergeUserId, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(conn);
        var kept = await ListAsync(conn, keepUserId, ct);
        var merged = await ListAsync(conn, mergeUserId, ct);
        var present = kept.Select(e => e.Eid).ToHashSet(StringComparer.Ordinal);
        var dropped = new List<string>();
        var next = kept.Count;

        foreach (var entry in merged) {
            if (present.Contains(entry.Eid)) continue;
            if (next >= EidRules.MaxEntries) {
                dropped.Add(entry.Eid);
                continue;
            }
            await InsertAsync(conn, keepUserId, entry.Eid, entry.Label, entry.Snapshot, next, ct);
            present.Add(entry.Eid);
            next++;
        }
        return dropped;
    }

    private static string? Reject(string eid, string? label, HashSet<string> existing) {
        if (!EidRules.IsValid(eid)) return EidRules.FormatReason;
        if (label?.Length > EidRules.MaxLabelLength) return EidRules.LabelReason;
        if (!existing.Contains(eid) && existing.Count >= EidRules.MaxEntries) return EidRules.CapReason;
        return null;
    }

    private static async Task LockUserAsync(NpgsqlConnection conn, Guid userId, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand("SELECT 1 FROM users WHERE user_id = $1 FOR UPDATE", conn);
        cmd.Parameters.AddWithValue(userId);
        await cmd.ExecuteScalarAsync(ct);
    }

    private static async Task<IReadOnlyList<ProfileEid>> ListAsync(NpgsqlConnection conn, Guid userId, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            "SELECT eid, label, snapshot, snapshot_at FROM user_eids WHERE user_id = $1 ORDER BY position", conn);
        cmd.Parameters.AddWithValue(userId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<ProfileEid>();
        while (await reader.ReadAsync(ct)) {
            results.Add(new ProfileEid {
                Eid = reader.GetString(0),
                Label = reader.IsDBNull(1) ? null : reader.GetString(1),
                Snapshot = reader.IsDBNull(2) ? null : JsonSerializer.Deserialize<EidSnapshot>(reader.GetString(2)),
                SnapshotAt = reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            });
        }
        return results;
    }

    private static async Task InsertAsync(NpgsqlConnection conn, Guid userId, string eid, string? label, EidSnapshot? snapshot, int position, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO user_eids (user_id, eid, position, label, snapshot, snapshot_at)
            VALUES ($1, $2, $3, $4, $5, CASE WHEN $5 IS NULL THEN NULL ELSE now() END)
            """, conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(eid);
        cmd.Parameters.AddWithValue(position);
        cmd.Parameters.AddWithValue((object?)label ?? DBNull.Value);
        cmd.Parameters.Add(SnapshotParameter(snapshot));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> WriteSnapshotAsync(NpgsqlConnection conn, Guid userId, string eid, EidSnapshot snapshot, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            "UPDATE user_eids SET snapshot = $3, snapshot_at = now() WHERE user_id = $1 AND eid = $2", conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(eid);
        cmd.Parameters.Add(SnapshotParameter(snapshot));
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    private static async Task CompactAsync(NpgsqlConnection conn, Guid userId, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE user_eids u SET position = r.rn - 1
            FROM (SELECT eid, row_number() OVER (ORDER BY position) AS rn FROM user_eids WHERE user_id = $1) r
            WHERE u.user_id = $1 AND u.eid = r.eid
            """, conn);
        cmd.Parameters.AddWithValue(userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task SetPositionAsync(NpgsqlConnection conn, Guid userId, string eid, int position, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand("UPDATE user_eids SET position = $3 WHERE user_id = $1 AND eid = $2", conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(eid);
        cmd.Parameters.AddWithValue(position);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static NpgsqlParameter SnapshotParameter(EidSnapshot? snapshot) => new() {
        Value = snapshot is null ? DBNull.Value : JsonSerializer.Serialize(snapshot),
        NpgsqlDbType = NpgsqlDbType.Jsonb,
    };
}
