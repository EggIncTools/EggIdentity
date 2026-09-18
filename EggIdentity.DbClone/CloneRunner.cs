using System.Globalization;
using Npgsql;

namespace EggIdentity.DbClone;

public sealed record CloneStamp(string App, string SourceDatabase, DateTimeOffset ClonedAt);

public static class CloneRunner {
    private const int CopyBufferChars = 1 << 16;

    private static readonly string StampTable = SqlIdentifier.Quote(PlanValidator.StampTable);

    public static async Task<IReadOnlyList<CloneTableCount>> RunAsync(
        ClonePlan plan, string sourceConn, string targetConn, bool dryRun, IProgress<CloneEvent> progress,
        CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(progress);

        var endpoints = CloneGuards.Check(plan, sourceConn, targetConn);
        Emit(progress, ClonePhase.Guard, $"source {endpoints.SourceDatabase} (read-only) to target {endpoints.TargetDatabase}");

        await using var source = new NpgsqlConnection(endpoints.SourceConnection);
        await source.OpenAsync(ct);
        await using var target = new NpgsqlConnection(targetConn);
        await target.OpenAsync(ct);

        var validated = await PlanValidator.ValidateAsync(plan, source, target, ct);
        foreach (var report in validated.Columns) {
            if (report.SourceOnly.Count > 0)
                Emit(progress, ClonePhase.Validate, $"source-only columns skipped: {string.Join(", ", report.SourceOnly)}", report.Table);
            if (report.TargetOnly.Count > 0)
                Emit(progress, ClonePhase.Validate, $"target-only columns left at default: {string.Join(", ", report.TargetOnly)}", report.Table);
        }
        Emit(progress, ClonePhase.Validate, $"{validated.Governed.Count} governed table(s), {plan.IgnoredTables.Count + 1} ignored");

        var order = LoadOrder.Sort(
            [.. validated.Governed.Select(t => t.Table)], await LoadOrder.ForeignKeysAsync(target, ct));
        Emit(progress, ClonePhase.Order, string.Join(" > ", order));

        var counts = await CountAsync(plan, order, source, target, ct);
        foreach (var c in counts)
            Emit(progress, ClonePhase.Plan, $"{c.Policy}: source {c.SourceRows}, target {c.TargetRows}", c.Table);

        if (dryRun) {
            Emit(progress, ClonePhase.Done, "dry run, nothing written");
            return counts;
        }

        await using (var tx = await target.BeginTransactionAsync(ct)) {
            await TruncateAsync(target, order, progress, ct);
            foreach (var table in order) {
                var policy = plan.Find(table)!;
                if (!policy.Copies) continue;
                var columns = validated.ColumnsFor(table)!.Columns;
                await CopyAsync(source, target, policy, columns, progress, ct);
                if (!string.IsNullOrWhiteSpace(policy.ScrubSql)) {
                    var scrubbed = await ExecAsync(target, policy.ScrubSql, ct);
                    Emit(progress, ClonePhase.Scrub, $"scrubbed {scrubbed} row(s)", table);
                }
            }
            await StampAsync(target, plan.App, endpoints.SourceDatabase, ct);
            await tx.CommitAsync(ct);
        }
        Emit(progress, ClonePhase.Stamp, $"stamped {plan.App} from {endpoints.SourceDatabase}");

        var final = await CountAsync(plan, order, source, target, ct);
        Emit(progress, ClonePhase.Done, $"cloned {final.Count(c => c.Policy is ClonePolicy.Full or ClonePolicy.Scrub)} table(s)");
        return final;
    }

    public static async Task<CloneStamp?> LastStampAsync(string targetConn, CancellationToken ct) {
        await using var conn = new NpgsqlConnection(targetConn);
        await conn.OpenAsync(ct);
        await using var exists = new NpgsqlCommand($"SELECT to_regclass('public.{PlanValidator.StampTable}') IS NOT NULL", conn);
        if (await exists.ExecuteScalarAsync(ct) is not true) return null;

        await using var cmd = new NpgsqlCommand(
            $"SELECT app, source_database, cloned_at FROM {StampTable} ORDER BY cloned_at DESC LIMIT 1", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new CloneStamp(reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateTime>(2));
    }

    internal static string CountSql(TablePolicy policy) =>
        $"SELECT count(*) FROM {SqlIdentifier.Quote(policy.Table)}{WhereClause(policy)}";

    internal static string ExportSql(TablePolicy policy, IReadOnlyList<string> columns) =>
        $"COPY (SELECT {SqlIdentifier.List(columns)} FROM {SqlIdentifier.Quote(policy.Table)}{WhereClause(policy)}) TO STDOUT";

    internal static string ImportSql(string table, IReadOnlyList<string> columns) =>
        $"COPY {SqlIdentifier.Quote(table)} ({SqlIdentifier.List(columns)}) FROM STDIN";

    internal static string TruncateSql(IEnumerable<string> tables) => $"TRUNCATE TABLE {SqlIdentifier.List(tables)}";

    private static string WhereClause(TablePolicy policy) =>
        string.IsNullOrWhiteSpace(policy.Where) ? "" : " WHERE " + policy.Where.Trim();

    private static async Task<List<CloneTableCount>> CountAsync(
        ClonePlan plan, IReadOnlyList<string> order, NpgsqlConnection source, NpgsqlConnection target, CancellationToken ct) {
        var counts = new List<CloneTableCount>(order.Count);
        foreach (var table in order) {
            var policy = plan.Find(table)!;
            var sourceRows = policy.Copies ? await CountRowsAsync(source, CountSql(policy), ct) : 0;
            var targetRows = await CountRowsAsync(target, CountSql(policy with { Where = null }), ct);
            counts.Add(new CloneTableCount(table, policy.Policy, sourceRows, targetRows));
        }
        return counts;
    }

    private static async Task TruncateAsync(
        NpgsqlConnection target, IReadOnlyList<string> order, IProgress<CloneEvent> progress, CancellationToken ct) {
        if (order.Count == 0) return;
        await using var cmd = new NpgsqlCommand(TruncateSql(order), target);
        await cmd.ExecuteNonQueryAsync(ct);
        Emit(progress, ClonePhase.Truncate, $"truncated {order.Count} table(s)");
    }

    private static async Task CopyAsync(
        NpgsqlConnection source, NpgsqlConnection target, TablePolicy policy, IReadOnlyList<string> columns,
        IProgress<CloneEvent> progress, CancellationToken ct) {
        long chars = 0;
        await using (var reader = await source.BeginTextExportAsync(ExportSql(policy, columns), ct))
        await using (var writer = await target.BeginTextImportAsync(ImportSql(policy.Table, columns), ct)) {
            var buffer = new char[CopyBufferChars];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0) {
                await writer.WriteAsync(buffer.AsMemory(0, read), ct);
                chars += read;
            }
        }
        Emit(progress, ClonePhase.Copy, $"copied {columns.Count} column(s), {chars.ToString(CultureInfo.InvariantCulture)} chars", policy.Table);
    }

    private static async Task StampAsync(NpgsqlConnection target, string app, string sourceDatabase, CancellationToken ct) {
        await using (var create = new NpgsqlCommand(
            $"CREATE TABLE IF NOT EXISTS {StampTable} (app TEXT NOT NULL, source_database TEXT NOT NULL, cloned_at TIMESTAMPTZ NOT NULL DEFAULT now())",
            target)) {
            await create.ExecuteNonQueryAsync(ct);
        }
        await using var insert = new NpgsqlCommand(
            $"INSERT INTO {StampTable} (app, source_database, cloned_at) VALUES ($1, $2, now())", target);
        insert.Parameters.AddWithValue(app);
        insert.Parameters.AddWithValue(sourceDatabase);
        await insert.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> CountRowsAsync(NpgsqlConnection conn, string sql, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    private static async Task<int> ExecAsync(NpgsqlConnection conn, string sql, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void Emit(IProgress<CloneEvent> progress, string phase, string message, string? table = null) =>
        progress.Report(new CloneEvent(DateTimeOffset.UtcNow, phase, message) { Table = table });
}
