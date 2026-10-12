using Npgsql;

namespace EggIdentity.Fleet;

public sealed record PinState(
    string App, string Upstream, string? PendingVersion, string? PendingCommit, string? LastError, DateTimeOffset UpdatedAt);

public interface IPinStates {
    Task<IReadOnlyList<PinState>> GetAllAsync(CancellationToken ct);
    Task SetPendingAsync(string app, string upstream, string version, string commit, CancellationToken ct);
    Task ClearPendingAsync(string app, string upstream, CancellationToken ct);
    Task SetErrorAsync(string app, string upstream, string message, CancellationToken ct);
}

public sealed class PinStateStore(NpgsqlDataSource dataSource) : IPinStates {
    public async Task<IReadOnlyList<PinState>> GetAllAsync(CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            SELECT app, upstream, pending_version, pending_commit, last_error, updated_at
            FROM fleet_pin_state
            """, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<PinState>();
        while (await reader.ReadAsync(ct)) {
            results.Add(new PinState(
                reader.GetString(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }
        return results;
    }

    public Task SetPendingAsync(string app, string upstream, string version, string commit, CancellationToken ct) =>
        UpsertAsync(app, upstream, version, commit, null, ct);

    public Task ClearPendingAsync(string app, string upstream, CancellationToken ct) =>
        UpsertAsync(app, upstream, null, null, null, ct);

    public Task SetErrorAsync(string app, string upstream, string message, CancellationToken ct) =>
        UpsertAsync(app, upstream, null, null, message, ct);

    private async Task UpsertAsync(
        string app, string upstream, string? version, string? commit, string? error, CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO fleet_pin_state (app, upstream, pending_version, pending_commit, last_error, updated_at)
            VALUES ($1, $2, $3, $4, $5, now())
            ON CONFLICT (app, upstream) DO UPDATE
            SET pending_version = $3, pending_commit = $4, last_error = $5, updated_at = now()
            """, conn);
        cmd.Parameters.AddWithValue(app);
        cmd.Parameters.AddWithValue(upstream);
        cmd.Parameters.AddWithValue((object?)version ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)commit ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)error ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
