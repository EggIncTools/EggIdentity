using Npgsql;

namespace EggIdentity;

public sealed class ExpiredRowSweeper(NpgsqlDataSource dataSource, TimeSpan interval) {
    public static readonly TimeSpan RevocationRetention = TimeSpan.FromDays(90);

    public async Task RunAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(interval);
        try {
            while (await timer.WaitForNextTickAsync(ct))
                await SweepAsync(ct);
        } catch (OperationCanceledException) { /* shutdown */ }
    }

    public async Task SweepAsync(CancellationToken ct) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using (var cmd = new NpgsqlCommand("DELETE FROM oauth_states WHERE expires_at < now()", conn))
            await cmd.ExecuteNonQueryAsync(ct);
        await using (var cmd = new NpgsqlCommand("DELETE FROM login_codes WHERE expires_at < now()", conn))
            await cmd.ExecuteNonQueryAsync(ct);
        await using (var cmd = new NpgsqlCommand(
            "DELETE FROM revoked_sessions WHERE revoked_at < now() - $1", conn)) {
            cmd.Parameters.AddWithValue(RevocationRetention);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
