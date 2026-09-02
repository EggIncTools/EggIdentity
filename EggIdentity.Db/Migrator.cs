using System.Reflection;
using Npgsql;

namespace EggIdentity.Db;

public static class Migrator {
    public static int PrefixNum(string path) {
        var baseName = Path.GetFileName(path);
        var idx = baseName.IndexOf('_');
        if (idx <= 0) return 0;
        return int.TryParse(baseName[..idx], out var n) ? n : 0;
    }

    public static IReadOnlyList<string> MigrationFiles(string dir) {
        return Directory.EnumerateFiles(dir)
            .Where(f => f.EndsWith(".up.sql", StringComparison.Ordinal))
            .OrderBy(PrefixNum)
            .ToList();
    }

    public static IReadOnlyList<string> EmbeddedMigrations(Assembly assembly, string resourcePrefix) {
        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.Ordinal)
                && n.EndsWith(".up.sql", StringComparison.Ordinal))
            .OrderBy(n => PrefixNum(n[resourcePrefix.Length..]))
            .ToList();
    }

    public static Task MigrateAsync(NpgsqlConnection conn, string dir, CancellationToken ct = default) =>
        MigrateAsync(conn, dir, "eggidentity_migrations", ct);

    public static Task MigrateAsync(NpgsqlConnection conn, string dir, string tableName, CancellationToken ct = default) {
        var steps = MigrationFiles(dir)
            .Select(f => (Version: PrefixNum(f), Load: (Func<CancellationToken, Task<string>>)(c => File.ReadAllTextAsync(f, c))))
            .ToList();
        return ApplyAsync(conn, tableName, steps, ct);
    }

    public static Task MigrateEmbeddedAsync(
        NpgsqlConnection conn, Assembly assembly, string resourcePrefix, string tableName, CancellationToken ct = default) {
        var steps = EmbeddedMigrations(assembly, resourcePrefix)
            .Select(n => (
                Version: PrefixNum(n[resourcePrefix.Length..]),
                Load: (Func<CancellationToken, Task<string>>)(_ => ReadResourceAsync(assembly, n))))
            .ToList();
        return ApplyAsync(conn, tableName, steps, ct);
    }

    private static async Task<string> ReadResourceAsync(Assembly assembly, string name) {
        await using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"embedded migration {name} not found");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task ApplyAsync(
        NpgsqlConnection conn, string tableName,
        IReadOnlyList<(int Version, Func<CancellationToken, Task<string>> Load)> steps, CancellationToken ct) {
        await using (var create = new NpgsqlCommand(
            $"CREATE TABLE IF NOT EXISTS {tableName} (version INTEGER PRIMARY KEY)", conn)) {
            await create.ExecuteNonQueryAsync(ct);
        }

        int current;
        await using (var q = new NpgsqlCommand(
            $"SELECT COALESCE(MAX(version), 0) FROM {tableName}", conn)) {
            current = Convert.ToInt32(await q.ExecuteScalarAsync(ct));
        }

        foreach (var (v, load) in steps) {
            if (v <= current) continue;
            var sql = await load(ct);

            await using var tx = await conn.BeginTransactionAsync(ct);
            await using (var exec = new NpgsqlCommand(sql, conn))
                await exec.ExecuteNonQueryAsync(ct);
            await using (var rec = new NpgsqlCommand(
                $"INSERT INTO {tableName} (version) VALUES ($1)", conn)) {
                rec.Parameters.Add(new NpgsqlParameter { Value = v });
                await rec.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            current = v;
        }
    }
}
