using System.Reflection;
using EggIdentity.Db;
using Npgsql;

namespace EggIdentity.Settings.Store;

public sealed record StoredSetting(string Key, string? Value, DateTimeOffset UpdatedAt, string? UpdatedBy);

public sealed class SettingsStore(NpgsqlDataSource dataSource, SecretProtector? protector = null) {
    private const string MigrationsTable = "eggidentity_settings_migrations";
    private const string ResourcePrefix = "EggIdentity.Settings.Store.Migrations.";

    public bool CanStoreSecrets => protector is not null;

    public static Task MigrateAsync(NpgsqlConnection conn, CancellationToken ct = default) =>
        Migrator.MigrateEmbeddedAsync(conn, Assembly.GetExecutingAssembly(), ResourcePrefix, MigrationsTable, ct);

    public async Task MigrateAsync(CancellationToken ct = default) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await MigrateAsync(conn, ct);
    }

    public async Task<IReadOnlyDictionary<string, string?>> GetAllAsync(CancellationToken ct = default) {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT key, value FROM app_settings", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) {
            var key = reader.GetString(0);
            var raw = reader.IsDBNull(1) ? null : reader.GetString(1);
            result[key] = Reveal(raw);
        }
        return result;
    }

    public async Task<StoredSetting?> GetAsync(string key, CancellationToken ct = default) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT key, value, updated_at, updated_by FROM app_settings WHERE key = $1", conn);
        cmd.Parameters.AddWithValue(key);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new StoredSetting(
            reader.GetString(0),
            Reveal(reader.IsDBNull(1) ? null : reader.GetString(1)),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    public async Task SetAsync(SettingDescriptor descriptor, string? value, string? updatedBy, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (descriptor.IsSecret && !CanStoreSecrets) {
            throw new InvalidOperationException(
                $"cannot store secret \"{descriptor.Key}\": EGGIDENTITY_SETTINGS_KEY is not configured");
        }

        var stored = descriptor.IsSecret && !string.IsNullOrEmpty(value) ? protector!.Protect(value) : value;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO app_settings (key, value, updated_at, updated_by)
            VALUES ($1, $2, now(), $3)
            ON CONFLICT (key) DO UPDATE SET
                value = EXCLUDED.value,
                updated_at = now(),
                updated_by = EXCLUDED.updated_by
            """, conn);
        cmd.Parameters.AddWithValue(descriptor.Key);
        cmd.Parameters.AddWithValue((object?)stored ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)updatedBy ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("DELETE FROM app_settings WHERE key = $1", conn);
        cmd.Parameters.AddWithValue(key);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task NotifyChangedAsync(CancellationToken ct = default) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("NOTIFY eggidentity_settings", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private string? Reveal(string? raw) {
        if (!SecretProtector.IsProtected(raw)) return raw;
        return protector?.Unprotect(raw);
    }
}
