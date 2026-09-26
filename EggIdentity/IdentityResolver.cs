using EggIdentity.Contract;
using Npgsql;

namespace EggIdentity;

public sealed record ResolveResult(Guid UserId, string Role, string? DiscordId, bool IsNew, IReadOnlyList<Guid> MergedUserIds) {
    public ResolveResult(Guid userId, string role, string? discordId, bool isNew) : this(userId, role, discordId, isNew, []) { }
}

public sealed record LinkOutcome(
    bool Linked, bool Conflict, string? ConflictUsername, DateTimeOffset? ConflictCreatedAt,
    bool AlreadyLinked = false, bool NotAvailable = false, Guid? ConflictUserId = null);

public sealed class IdentityResolver(NpgsqlDataSource dataSource, AdminAllowlist allowlist) {
    public Task<ResolveResult> ResolveAsync(
        string provider, string subject, string? discordId, string? username, string? avatar, CancellationToken ct) =>
        ResolveWithSourcesAsync(provider, subject, new Dictionary<string, string?> { [IdentityWire.Discord] = discordId }, username, avatar, ct);

    public async Task<ResolveResult> ResolveWithSourcesAsync(
        string provider, string subject, IReadOnlyDictionary<string, string?> sourceIds, string? username, string? avatar, CancellationToken ct) {
        var sources = NormalizeSources(provider, subject, sourceIds);
        var discordId = sources.GetValueOrDefault(IdentityWire.Discord);

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var candidates = await CollectCandidatesAsync(conn, provider, subject, sources, discordId, ct);
        var isNew = candidates.Count == 0;
        var userId = isNew ? Guid.NewGuid() : candidates[0];
        var merged = new List<Guid>();
        foreach (var loser in candidates.Skip(1)) {
            await MergeIntoAsync(conn, userId, loser, ct);
            merged.Add(loser);
        }

        var role = await UpsertUserAsync(conn, userId, discordId, username, avatar, isNew, ct);
        if (!isNew) await DeleteStaleIdentityAsync(conn, userId, provider, subject, ct);
        var winnerId = await InsertIdentityAsync(conn, userId, provider, subject, username, avatar, ct);
        if (winnerId != userId) {
            if (isNew) await DeleteUserAsync(conn, userId, ct);
            userId = winnerId;
            isNew = false;
            role = await ExistingRoleOrDefaultAsync(conn, userId, discordId, allowlist, ct);
        } else if (!isNew) {
            await UpdateIdentitySnapshotAsync(conn, provider, subject, username, avatar, ct);
        }

        foreach (var (sourceProvider, sourceSubject) in sources) {
            if (sourceProvider == provider) continue;
            await InsertIdentityAsync(conn, userId, sourceProvider, sourceSubject, null, null, ct);
        }

        await tx.CommitAsync(ct);
        return new ResolveResult(userId, role, discordId, isNew, merged);
    }

    public async Task<Guid> MergeAsync(Guid keepUserId, Guid mergeUserId, CancellationToken ct) {
        if (keepUserId == mergeUserId) return keepUserId;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await MergeIntoAsync(conn, keepUserId, mergeUserId, ct);
        await tx.CommitAsync(ct);
        return keepUserId;
    }

    public async Task<Guid> MergeOldestAsync(Guid a, Guid b, CancellationToken ct) {
        if (a == b) return a;
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var ordered = await OrderByCreationAsync(conn, [a, b], ct);
        if (ordered.Count < 2) {
            await tx.CommitAsync(ct);
            return ordered.Count == 1 ? ordered[0] : a;
        }
        await MergeIntoAsync(conn, ordered[0], ordered[1], ct);
        await tx.CommitAsync(ct);
        return ordered[0];
    }

    public async Task<LinkOutcome> TryLinkAsync(
        Guid userId, string provider, string subject, string? discordId, string? username, string? avatar, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var existingOwner = await LookupAsync(conn, provider, subject, ct);
        if (existingOwner is { } ownerId && ownerId != userId) {
            await using var cmd = new NpgsqlCommand("SELECT username, created_at FROM users WHERE user_id = $1", conn);
            cmd.Parameters.AddWithValue(ownerId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            string? ownerUsername = null;
            DateTimeOffset? ownerCreatedAt = null;
            if (await reader.ReadAsync(ct)) {
                ownerUsername = reader.GetString(0);
                ownerCreatedAt = reader.GetFieldValue<DateTimeOffset>(1);
            }
            await tx.CommitAsync(ct);
            return new LinkOutcome(Linked: false, Conflict: true, ownerUsername, ownerCreatedAt, ConflictUserId: ownerId);
        }

        if (await LookupOtherSubjectAsync(conn, userId, provider, subject, ct)) {
            await tx.CommitAsync(ct);
            return new LinkOutcome(Linked: false, Conflict: false, null, null, AlreadyLinked: true);
        }

        try {
            await using var cmd = new NpgsqlCommand(
                """
                INSERT INTO identities (user_id, provider, subject, username, avatar)
                VALUES ($1, $2, $3, $4, $5)
                ON CONFLICT (provider, subject) DO UPDATE SET
                    username = COALESCE(EXCLUDED.username, identities.username),
                    avatar = COALESCE(EXCLUDED.avatar, identities.avatar)
                """, conn);
            cmd.Parameters.AddWithValue(userId);
            cmd.Parameters.AddWithValue(provider);
            cmd.Parameters.AddWithValue(subject);
            cmd.Parameters.AddWithValue((object?)username ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)avatar ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        } catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation) {
            return new LinkOutcome(Linked: false, Conflict: false, null, null, AlreadyLinked: true);
        }

        if (!string.IsNullOrEmpty(discordId)) {
            await using var cmd = new NpgsqlCommand(
                "UPDATE users SET discord_id = $2 WHERE user_id = $1 AND discord_id IS NULL", conn);
            cmd.Parameters.AddWithValue(userId);
            cmd.Parameters.AddWithValue(discordId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return new LinkOutcome(Linked: true, Conflict: false, null, null);
    }

    public async Task<IReadOnlyList<(string Provider, LinkOutcome Outcome)>> SyncSourceIdentitiesAsync(
        Guid userId, IReadOnlyDictionary<string, string?> perSourceIds, CancellationToken ct) {
        var results = new List<(string, LinkOutcome)>();
        foreach (var (provider, subject) in perSourceIds) {
            if (string.IsNullOrEmpty(subject)) {
                results.Add((provider, new LinkOutcome(Linked: false, Conflict: false, null, null, NotAvailable: true)));
                continue;
            }
            var discordId = provider == IdentityWire.Discord ? subject : null;
            var outcome = await TryLinkAsync(userId, provider, subject, discordId, null, null, ct);
            results.Add((provider, outcome));
        }
        return results;
    }

    public static Dictionary<string, string> NormalizeSources(
        string provider, string subject, IReadOnlyDictionary<string, string?> sourceIds) {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in sourceIds) {
            if (!string.IsNullOrEmpty(value) && IdentityWire.KnownProviders.Contains(key)) sources[key] = value;
        }
        if (IdentityWire.KnownProviders.Contains(provider)) sources[provider] = subject;
        return sources;
    }

    private static async Task<IReadOnlyList<Guid>> CollectCandidatesAsync(
        NpgsqlConnection conn, string provider, string subject, IReadOnlyDictionary<string, string> sources,
        string? discordId, CancellationToken ct) {
        var found = new HashSet<Guid>();
        if (await LookupAsync(conn, provider, subject, ct) is { } direct) found.Add(direct);
        foreach (var (sourceProvider, sourceSubject) in sources) {
            if (await LookupAsync(conn, sourceProvider, sourceSubject, ct) is { } owner) found.Add(owner);
        }
        if (!string.IsNullOrEmpty(discordId) && await LookupByDiscordIdAsync(conn, discordId, ct) is { } legacy) found.Add(legacy);
        return found.Count <= 1 ? [.. found] : await OrderByCreationAsync(conn, [.. found], ct);
    }

    private static async Task<IReadOnlyList<Guid>> OrderByCreationAsync(NpgsqlConnection conn, Guid[] ids, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id FROM users WHERE user_id = ANY($1) ORDER BY created_at, user_id", conn);
        cmd.Parameters.AddWithValue(ids);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var ordered = new List<Guid>();
        while (await reader.ReadAsync(ct)) ordered.Add(reader.GetGuid(0));
        return ordered;
    }

    private static async Task MergeIntoAsync(NpgsqlConnection conn, Guid keepUserId, Guid mergeUserId, CancellationToken ct) {
        string? loserDiscordId = null;
        string loserUsername = "";
        string? loserAvatar = null;
        var loserRole = UserRoles.ToName(UserRole.Viewer);
        string? loserTimezone = null;
        string? loserLanguage = null;
        string? loserTheme = null;
        await using (var read = new NpgsqlCommand(
            "SELECT discord_id, username, avatar, role, timezone, language, theme::text FROM users WHERE user_id = $1", conn)) {
            read.Parameters.AddWithValue(mergeUserId);
            await using var reader = await read.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return;
            loserDiscordId = reader.IsDBNull(0) ? null : reader.GetString(0);
            loserUsername = reader.GetString(1);
            loserAvatar = reader.IsDBNull(2) ? null : reader.GetString(2);
            loserRole = reader.GetString(3);
            loserTimezone = reader.IsDBNull(4) ? null : reader.GetString(4);
            loserLanguage = reader.IsDBNull(5) ? null : reader.GetString(5);
            loserTheme = reader.IsDBNull(6) ? null : reader.GetString(6);
        }

        await using (var cmd = new NpgsqlCommand("UPDATE users SET discord_id = NULL WHERE user_id = $1", conn)) {
            cmd.Parameters.AddWithValue(mergeUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand(
            """
            UPDATE users SET
                discord_id = COALESCE(discord_id, $2),
                username = CASE WHEN username = '' THEN $3 ELSE username END,
                avatar = CASE WHEN avatar_is_custom THEN avatar ELSE COALESCE(avatar, $4) END,
                role = CASE
                    WHEN role = 'admin' OR $5 = 'admin' THEN 'admin'
                    WHEN role = 'contributor' OR $5 = 'contributor' THEN 'contributor'
                    ELSE role END,
                timezone = COALESCE(timezone, $6),
                language = COALESCE(language, $7),
                theme = COALESCE(theme, $8::jsonb)
            WHERE user_id = $1
            """, conn)) {
            cmd.Parameters.AddWithValue(keepUserId);
            cmd.Parameters.AddWithValue((object?)loserDiscordId ?? DBNull.Value);
            cmd.Parameters.AddWithValue(loserUsername);
            cmd.Parameters.AddWithValue((object?)loserAvatar ?? DBNull.Value);
            cmd.Parameters.AddWithValue(loserRole);
            cmd.Parameters.AddWithValue((object?)loserTimezone ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)loserLanguage ?? DBNull.Value);
            cmd.Parameters.AddWithValue((object?)loserTheme ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand(
            "DELETE FROM identities WHERE user_id = $2 AND provider IN (SELECT provider FROM identities WHERE user_id = $1)", conn)) {
            cmd.Parameters.AddWithValue(keepUserId);
            cmd.Parameters.AddWithValue(mergeUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand("UPDATE identities SET user_id = $1 WHERE user_id = $2", conn)) {
            cmd.Parameters.AddWithValue(keepUserId);
            cmd.Parameters.AddWithValue(mergeUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO github_sponsor_status (user_id, is_sponsor, last_synced_at, updated_at)
            SELECT $1, is_sponsor, last_synced_at, updated_at FROM github_sponsor_status WHERE user_id = $2
            ON CONFLICT (user_id) DO NOTHING
            """, conn)) {
            cmd.Parameters.AddWithValue(keepUserId);
            cmd.Parameters.AddWithValue(mergeUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand(
            """
            INSERT INTO cookie_consent (user_id, functional, analytics, policy_version, decided_at, updated_at)
            SELECT $1, functional, analytics, policy_version, decided_at, updated_at FROM cookie_consent WHERE user_id = $2
            ON CONFLICT (user_id) DO NOTHING
            """, conn)) {
            cmd.Parameters.AddWithValue(keepUserId);
            cmd.Parameters.AddWithValue(mergeUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand("UPDATE user_merges SET kept_user_id = $1 WHERE kept_user_id = $2", conn)) {
            cmd.Parameters.AddWithValue(keepUserId);
            cmd.Parameters.AddWithValue(mergeUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO user_merges (merged_user_id, kept_user_id) VALUES ($2, $1) ON CONFLICT (merged_user_id) DO UPDATE SET kept_user_id = EXCLUDED.kept_user_id, merged_at = now()", conn)) {
            cmd.Parameters.AddWithValue(keepUserId);
            cmd.Parameters.AddWithValue(mergeUserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await DeleteUserAsync(conn, mergeUserId, ct);
    }

    private static async Task DeleteUserAsync(NpgsqlConnection conn, Guid userId, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand("DELETE FROM users WHERE user_id = $1", conn);
        cmd.Parameters.AddWithValue(userId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid?> LookupAsync(NpgsqlConnection conn, string provider, string subject, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            "SELECT user_id FROM identities WHERE provider = $1 AND subject = $2", conn);
        cmd.Parameters.AddWithValue(provider);
        cmd.Parameters.AddWithValue(subject);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private static async Task<bool> LookupOtherSubjectAsync(NpgsqlConnection conn, Guid userId, string provider, string subject, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            "SELECT 1 FROM identities WHERE user_id = $1 AND provider = $2 AND subject != $3", conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(provider);
        cmd.Parameters.AddWithValue(subject);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<Guid?> LookupByDiscordIdAsync(NpgsqlConnection conn, string discordId, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand("SELECT user_id FROM users WHERE discord_id = $1", conn);
        cmd.Parameters.AddWithValue(discordId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is Guid g ? g : null;
    }

    private async Task<string> UpsertUserAsync(
        NpgsqlConnection conn, Guid userId, string? discordId, string? username, string? avatar, bool isNew, CancellationToken ct) {
        var role = isNew
            ? ResolveNewUserRole(discordId, allowlist)
            : await ExistingRoleOrDefaultAsync(conn, userId, discordId, allowlist, ct);

        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO users (user_id, discord_id, username, avatar, role, last_login_at)
            VALUES ($1, $2, $3, $4, $5, now())
            ON CONFLICT (user_id) DO UPDATE SET
                discord_id = COALESCE(users.discord_id, EXCLUDED.discord_id),
                username = COALESCE(NULLIF(EXCLUDED.username, ''), users.username),
                avatar = CASE WHEN users.avatar_is_custom THEN users.avatar ELSE COALESCE(EXCLUDED.avatar, users.avatar) END,
                role = EXCLUDED.role,
                last_login_at = now()
            """, conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue((object?)discordId ?? DBNull.Value);
        cmd.Parameters.AddWithValue(username ?? "");
        cmd.Parameters.AddWithValue((object?)avatar ?? DBNull.Value);
        cmd.Parameters.AddWithValue(role);
        await cmd.ExecuteNonQueryAsync(ct);
        return role;
    }

    private static async Task<string> ExistingRoleOrDefaultAsync(
        NpgsqlConnection conn, Guid userId, string? discordId, AdminAllowlist allowlist, CancellationToken ct) {
        if (!string.IsNullOrEmpty(discordId) && allowlist.Ids.Contains(discordId))
            return UserRoles.ToName(UserRole.Admin);

        await using var cmd = new NpgsqlCommand("SELECT role FROM users WHERE user_id = $1", conn);
        cmd.Parameters.AddWithValue(userId);
        var existing = await cmd.ExecuteScalarAsync(ct) as string;
        return existing ?? UserRoles.ToName(UserRole.Viewer);
    }

    private static string ResolveNewUserRole(string? discordId, AdminAllowlist allowlist) =>
        !string.IsNullOrEmpty(discordId) && allowlist.Ids.Contains(discordId)
            ? UserRoles.ToName(UserRole.Admin)
            : UserRoles.ToName(UserRole.Viewer);

    private static async Task DeleteStaleIdentityAsync(
        NpgsqlConnection conn, Guid userId, string provider, string subject, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM identities WHERE user_id = $1 AND provider = $2 AND subject <> $3", conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(provider);
        cmd.Parameters.AddWithValue(subject);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Guid> InsertIdentityAsync(
        NpgsqlConnection conn, Guid userId, string provider, string subject, string? username, string? avatar, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO identities (user_id, provider, subject, username, avatar)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (provider, subject) DO NOTHING
            """, conn);
        cmd.Parameters.AddWithValue(userId);
        cmd.Parameters.AddWithValue(provider);
        cmd.Parameters.AddWithValue(subject);
        cmd.Parameters.AddWithValue((object?)username ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)avatar ?? DBNull.Value);
        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected > 0) return userId;

        var winner = await LookupAsync(conn, provider, subject, ct);
        return winner ?? userId;
    }

    private static async Task UpdateIdentitySnapshotAsync(
        NpgsqlConnection conn, string provider, string subject, string? username, string? avatar, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(
            "UPDATE identities SET username = COALESCE($3, username), avatar = COALESCE($4, avatar) WHERE provider = $1 AND subject = $2",
            conn);
        cmd.Parameters.AddWithValue(provider);
        cmd.Parameters.AddWithValue(subject);
        cmd.Parameters.AddWithValue((object?)username ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)avatar ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
