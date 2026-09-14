using Npgsql;

namespace EggIdentity.Db;

public static class Database {
    public static async Task<NpgsqlConnection> InitAsync(string connStr, CancellationToken ct = default) {
        if (string.IsNullOrEmpty(connStr))
            throw new ArgumentException("Database.Init: empty connection string", nameof(connStr));
        if (DescribeUriForm(connStr) is { } uriError) throw new ArgumentException(uriError, nameof(connStr));
        var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);
        await using var ping = new NpgsqlCommand("SELECT 1", conn);
        await ping.ExecuteScalarAsync(ct);
        return conn;
    }

    public static string? DescribeUriForm(string connStr) {
        if (string.IsNullOrEmpty(connStr)) return null;
        var text = connStr.TrimStart();
        if (!text.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !text.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }
        return "connection string is a URI; Npgsql needs keyword form, "
            + "for example \"Host=frame;Port=5432;Username=app;Password=...;Database=app\"";
    }
}
