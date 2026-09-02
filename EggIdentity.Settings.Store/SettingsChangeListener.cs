using Npgsql;

namespace EggIdentity.Settings.Store;

public sealed class SettingsChangeListener(NpgsqlDataSource dataSource, SettingsCache cache) {
    public async Task RunAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            try {
                await using var conn = await dataSource.OpenConnectionAsync(ct);
                conn.Notification += (_, _) => cache.Invalidate();
                await using (var listen = new NpgsqlCommand("LISTEN eggidentity_settings", conn))
                    await listen.ExecuteNonQueryAsync(ct);

                while (!ct.IsCancellationRequested)
                    await conn.WaitAsync(ct);
            } catch (OperationCanceledException) {
                return;
            } catch (NpgsqlException) {
                await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
        }
    }
}
