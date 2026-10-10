using Npgsql;

namespace EggIdentity.DbClone;

public sealed record ColumnReport(
    string Table, IReadOnlyList<string> Columns, IReadOnlyList<string> SourceOnly, IReadOnlyList<string> TargetOnly);

public sealed record ValidatedPlan(ClonePlan Plan, IReadOnlyList<TablePolicy> Governed, IReadOnlyList<ColumnReport> Columns) {
    public ColumnReport? ColumnsFor(string table) =>
        Columns.FirstOrDefault(c => string.Equals(c.Table, table, StringComparison.Ordinal));
}

public static class PlanValidator {
    public const string StampTable = "eggidentity_clone_stamp";

    private const string TablesSql =
        "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'";

    private const string ColumnsSql =
        "SELECT table_name, column_name FROM information_schema.columns "
        + "WHERE table_schema = 'public' AND is_generated = 'NEVER' ORDER BY table_name, ordinal_position";

    public static IReadOnlyList<string> Coverage(
        ClonePlan plan, IReadOnlyCollection<string> sourceTables, IReadOnlyCollection<string> targetTables) {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sourceTables);
        ArgumentNullException.ThrowIfNull(targetTables);

        var source = new HashSet<string>(sourceTables, StringComparer.Ordinal);
        var target = new HashSet<string>(targetTables, StringComparer.Ordinal);
        var ignored = Ignored(plan);
        var planned = new HashSet<string>(StringComparer.Ordinal);
        var errors = new List<string>();

        foreach (var t in plan.Tables) {
            if (!planned.Add(t.Table)) errors.Add($"{t.Table} is listed twice");
            if (ignored.Contains(t.Table)) errors.Add($"{t.Table} is both governed and ignored");
            if (t.Policy == ClonePolicy.Scrub && string.IsNullOrWhiteSpace(t.ScrubSql))
                errors.Add($"{t.Table} is Scrub but has no ScrubSql");

            var inSource = source.Contains(t.Table);
            var inTarget = target.Contains(t.Table);
            if (!inSource && !inTarget) {
                errors.Add($"{t.Table} exists in neither database");
                continue;
            }
            if (t.Copies && !inSource) errors.Add($"{t.Table} is {t.Policy} but is missing from the source");
            if (t.Truncates && !inTarget) errors.Add($"{t.Table} is {t.Policy} but is missing from the target");
        }

        foreach (var t in target.Order(StringComparer.Ordinal)) {
            if (!planned.Contains(t) && !ignored.Contains(t)) errors.Add($"{t} exists on the target but has no policy");
        }
        return errors;
    }

    public static ColumnReport Intersect(string table, IReadOnlyList<string> sourceColumns, IReadOnlyList<string> targetColumns) {
        ArgumentNullException.ThrowIfNull(sourceColumns);
        ArgumentNullException.ThrowIfNull(targetColumns);
        var source = new HashSet<string>(sourceColumns, StringComparer.Ordinal);
        var target = new HashSet<string>(targetColumns, StringComparer.Ordinal);
        return new ColumnReport(
            table,
            [.. targetColumns.Where(source.Contains)],
            [.. sourceColumns.Where(c => !target.Contains(c))],
            [.. targetColumns.Where(c => !source.Contains(c))]);
    }

    public static async Task<ValidatedPlan> ValidateAsync(
        ClonePlan plan, NpgsqlConnection source, NpgsqlConnection target, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(plan);
        var sourceTables = await TablesAsync(source, ct);
        var targetTables = await TablesAsync(target, ct);
        var errors = new List<string>(Coverage(plan, sourceTables, targetTables));
        var reports = new List<ColumnReport>();

        if (errors.Count == 0) {
            var sourceColumns = await ColumnsAsync(source, ct);
            var targetColumns = await ColumnsAsync(target, ct);
            foreach (var t in plan.Tables.Where(t => t.Copies)) {
                var report = Intersect(
                    t.Table, sourceColumns.GetValueOrDefault(t.Table, []), targetColumns.GetValueOrDefault(t.Table, []));
                if (report.Columns.Count == 0) errors.Add($"{t.Table} shares no columns between source and target");
                reports.Add(report);
            }
        }

        return errors.Count > 0
            ? throw new InvalidOperationException("clone plan rejected: " + string.Join("; ", errors))
            : new ValidatedPlan(plan, [.. plan.Tables.Where(t => t.Truncates)], reports);
    }

    private static HashSet<string> Ignored(ClonePlan plan) =>
        new(plan.IgnoredTables.Append(StampTable), StringComparer.Ordinal);

    private static async Task<List<string>> TablesAsync(NpgsqlConnection conn, CancellationToken ct) {
        var tables = new List<string>();
        await using var cmd = new NpgsqlCommand(TablesSql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) tables.Add(reader.GetString(0));
        return tables;
    }

    private static async Task<Dictionary<string, List<string>>> ColumnsAsync(NpgsqlConnection conn, CancellationToken ct) {
        var columns = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        await using var cmd = new NpgsqlCommand(ColumnsSql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) {
            var table = reader.GetString(0);
            if (!columns.TryGetValue(table, out var list)) {
                list = [];
                columns[table] = list;
            }
            list.Add(reader.GetString(1));
        }
        return columns;
    }
}
