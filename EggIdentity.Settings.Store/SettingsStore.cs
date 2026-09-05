using System.Reflection;
using System.Text.Json;
using EggIdentity.Db;
using Npgsql;
using NpgsqlTypes;

namespace EggIdentity.Settings.Store;

public sealed record StoredSetting(string Key, string? Value, DateTimeOffset UpdatedAt, string? UpdatedBy);

public sealed class SettingsStore(NpgsqlDataSource dataSource, SecretProtector? protector = null) {
    private const string MigrationsTable = "eggidentity_settings_migrations";
    private const string ResourcePrefix = "EggIdentity.Settings.Store.Migrations.";
    private const string RowColumns = "collection, id, value, updated_at, updated_by";

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
            result[key] = Reveal(key, raw);
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
            Reveal(key, reader.IsDBNull(1) ? null : reader.GetString(1)),
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

    public async Task<IReadOnlyList<CollectionRow>> ListRowsAsync(string collection, CancellationToken ct = default) {
        var rows = new List<CollectionRow>();
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"SELECT {RowColumns} FROM app_setting_collections WHERE collection = $1 ORDER BY id", conn);
        cmd.Parameters.AddWithValue(collection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(ReadRow(reader));
        return rows;
    }

    public async Task<CollectionRow?> GetRowAsync(string collection, string id, CancellationToken ct = default) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"SELECT {RowColumns} FROM app_setting_collections WHERE collection = $1 AND id = $2", conn);
        cmd.Parameters.AddWithValue(collection);
        cmd.Parameters.AddWithValue(id);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRow(reader) : null;
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<CollectionRow>>> GetAllRowsAsync(CancellationToken ct = default) {
        var grouped = new Dictionary<string, List<CollectionRow>>(StringComparer.Ordinal);
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"SELECT {RowColumns} FROM app_setting_collections ORDER BY collection, id", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) {
            var row = ReadRow(reader);
            if (!grouped.TryGetValue(row.Collection, out var list)) {
                list = [];
                grouped[row.Collection] = list;
            }
            list.Add(row);
        }
        return grouped.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<CollectionRow>)kv.Value, StringComparer.Ordinal);
    }

    public async Task UpsertRowAsync(
        CollectionDescriptor descriptor, string id, IReadOnlyDictionary<string, string?> values,
        string? updatedBy, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (descriptor.HasSecrets && !CanStoreSecrets) {
            throw new InvalidOperationException(
                $"cannot store rows of \"{descriptor.Key}\": it has secret fields and EGGIDENTITY_SETTINGS_KEY is not configured");
        }

        var sealedValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in values) {
            var secret = descriptor.FindField(name) is { IsSecret: true } && !string.IsNullOrEmpty(value);
            sealedValues[name] = secret ? protector!.Protect(value!) : value;
        }
        var json = JsonSerializer.Serialize(sealedValues);

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO app_setting_collections (collection, id, value, updated_at, updated_by)
            VALUES ($1, $2, $3, now(), $4)
            ON CONFLICT (collection, id) DO UPDATE SET
                value = EXCLUDED.value,
                updated_at = now(),
                updated_by = EXCLUDED.updated_by
            """, conn);
        cmd.Parameters.AddWithValue(descriptor.Key);
        cmd.Parameters.AddWithValue(id);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, json);
        cmd.Parameters.AddWithValue((object?)updatedBy ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
        await NotifyChangedAsync(ct);
    }

    public async Task DeleteRowAsync(string collection, string id, CancellationToken ct = default) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM app_setting_collections WHERE collection = $1 AND id = $2", conn);
        cmd.Parameters.AddWithValue(collection);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
        await NotifyChangedAsync(ct);
    }

    public async Task NotifyChangedAsync(CancellationToken ct = default) {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("NOTIFY eggidentity_settings", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private CollectionRow ReadRow(NpgsqlDataReader reader) {
        var collection = reader.GetString(0);
        var id = reader.GetString(1);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        using (var doc = JsonDocument.Parse(reader.GetString(2))) {
            foreach (var prop in doc.RootElement.EnumerateObject()) {
                values[prop.Name] = Reveal($"{collection}[{id}].{prop.Name}", prop.Value.ValueKind switch {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => prop.Value.GetString(),
                    _ => prop.Value.GetRawText(),
                });
            }
        }
        return new CollectionRow(
            collection,
            id,
            values,
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.IsDBNull(4) ? null : reader.GetString(4));
    }

    private string? Reveal(string label, string? raw) {
        if (!SecretProtector.IsProtected(raw)) return raw;
        if (protector is null) {
            throw new InvalidOperationException(
                $"stored secret {label} cannot be read: EGGIDENTITY_SETTINGS_KEY is not configured");
        }
        return protector.Unprotect(raw) ?? throw new InvalidOperationException(
            $"stored secret {label} cannot be decrypted: EGGIDENTITY_SETTINGS_KEY does not match the key it was written with");
    }
}
