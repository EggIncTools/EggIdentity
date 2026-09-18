using Npgsql;

namespace EggIdentity.DbClone;

public sealed record ForeignKey(string Child, string Parent);

public static class LoadOrder {
    private const string ForeignKeysSql =
        "SELECT ch.relname, pa.relname FROM pg_constraint c "
        + "JOIN pg_class ch ON ch.oid = c.conrelid "
        + "JOIN pg_class pa ON pa.oid = c.confrelid "
        + "JOIN pg_namespace n ON n.oid = ch.relnamespace "
        + "WHERE c.contype = 'f' AND n.nspname = 'public'";

    public static IReadOnlyList<string> Sort(IReadOnlyList<string> tables, IReadOnlyList<ForeignKey> foreignKeys) {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(foreignKeys);

        var set = new HashSet<string>(tables, StringComparer.Ordinal);
        var indegree = tables.ToDictionary(t => t, _ => 0, StringComparer.Ordinal);
        var children = tables.ToDictionary(t => t, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var fk in foreignKeys) {
            if (string.Equals(fk.Child, fk.Parent, StringComparison.Ordinal)) continue;
            if (!set.Contains(fk.Child) || !set.Contains(fk.Parent)) continue;
            children[fk.Parent].Add(fk.Child);
            indegree[fk.Child]++;
        }

        var ordered = new List<string>(tables.Count);
        var ready = new Queue<string>(tables.Where(t => indegree[t] == 0));
        while (ready.TryDequeue(out var table)) {
            ordered.Add(table);
            foreach (var child in children[table]) {
                if (--indegree[child] == 0) ready.Enqueue(child);
            }
        }

        if (ordered.Count == tables.Count) return ordered;
        var placed = new HashSet<string>(ordered, StringComparer.Ordinal);
        var cycle = tables.Where(t => !placed.Contains(t));
        throw new InvalidOperationException("foreign keys form a cycle among: " + string.Join(", ", cycle));
    }

    public static async Task<IReadOnlyList<ForeignKey>> ForeignKeysAsync(NpgsqlConnection conn, CancellationToken ct) {
        var keys = new List<ForeignKey>();
        await using var cmd = new NpgsqlCommand(ForeignKeysSql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) keys.Add(new ForeignKey(reader.GetString(0), reader.GetString(1)));
        return keys;
    }
}
