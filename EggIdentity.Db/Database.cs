using System.Net.Sockets;
using EggIdentity.Resilience;
using Npgsql;

namespace EggIdentity.Db;

public static class Database {
    public static RetryOptions StartupRetry { get; } = new() {
        MaxAttempts = 8,
        BaseDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(15),
        ShouldRetry = IsTransient,
    };

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

    public static Task<NpgsqlConnection> WaitForAsync(
        NpgsqlDataSource source, TimeProvider? time = null, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(source);
        return Retry.RunAsync(async token => await source.OpenConnectionAsync(token), StartupRetry, time, ct);
    }

    public static bool IsTransient(Exception e) {
        ArgumentNullException.ThrowIfNull(e);
        for (var current = e; current is not null; current = current.InnerException) {
            if (current is SocketException or TimeoutException or NpgsqlException { IsTransient: true }) return true;
            if (current is PostgresException pg) return IsStarting(pg.SqlState);
        }
        return false;
    }

    private static bool IsStarting(string? sqlState) =>
        sqlState is PostgresErrorCodes.CannotConnectNow or PostgresErrorCodes.TooManyConnections;

    public static string? DescribeUriForm(string connStr) {
        if (string.IsNullOrEmpty(connStr)) return null;
        var text = connStr.TrimStart();
        return text.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? "connection string is a URI; Npgsql needs keyword form, "
                + "for example \"Host=frame;Port=5432;Username=app;Password=...;Database=app\""
            : null;
    }
}
