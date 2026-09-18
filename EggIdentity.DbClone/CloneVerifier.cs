using System.Globalization;
using Npgsql;

namespace EggIdentity.DbClone;

public sealed record VerifyResult(string Table, ClonePolicy Policy, bool Ok, string Detail);

public static class CloneVerifier {
    public static async Task<IReadOnlyList<VerifyResult>> VerifyAsync(
        ClonePlan plan, string sourceConn, string targetConn, IReadOnlyCollection<string>? userIds = null,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(plan);
        var endpoints = CloneGuards.Check(plan, sourceConn, targetConn);

        await using var source = new NpgsqlConnection(endpoints.SourceConnection);
        await source.OpenAsync(ct);
        await using var target = new NpgsqlConnection(targetConn);
        await target.OpenAsync(ct);
        await PlanValidator.ValidateAsync(plan, source, target, ct);

        var results = new List<VerifyResult>();
        foreach (var policy in plan.Tables) {
            switch (policy.Policy) {
                case ClonePolicy.Full:
                    results.Add(await CountsMatchAsync(policy, source, target, ct));
                    break;
                case ClonePolicy.Scrub:
                    results.Add(await ScrubbedAsync(policy, target, ct));
                    break;
                case ClonePolicy.SchemaOnly:
                    results.Add(await EmptyAsync(policy, target, ct));
                    break;
                case ClonePolicy.Skip:
                    break;
            }
            if (userIds is not null) {
                foreach (var column in policy.UserIdColumns)
                    results.Add(await OrphansAsync(policy, column, userIds, target, ct));
            }
        }
        return results;
    }

    internal static string OrphanSql(string table, string column) =>
        $"SELECT count(*) FROM {SqlIdentifier.Quote(table)} WHERE {SqlIdentifier.Quote(column)} IS NOT NULL "
        + $"AND NOT ({SqlIdentifier.Quote(column)}::text = ANY($1))";

    private static async Task<VerifyResult> CountsMatchAsync(
        TablePolicy policy, NpgsqlConnection source, NpgsqlConnection target, CancellationToken ct) {
        var expected = await ScalarAsync(source, CloneRunner.CountSql(policy), ct);
        var actual = await ScalarAsync(target, CloneRunner.CountSql(policy with { Where = null }), ct);
        return new VerifyResult(policy.Table, policy.Policy, expected == actual, $"source {expected}, target {actual}");
    }

    private static async Task<VerifyResult> ScrubbedAsync(TablePolicy policy, NpgsqlConnection target, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(policy.VerifySql))
            return new VerifyResult(policy.Table, policy.Policy, true, "no VerifySql");
        await using var cmd = new NpgsqlCommand(policy.VerifySql, target);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = 0;
        while (await reader.ReadAsync(ct)) rows++;
        return new VerifyResult(policy.Table, policy.Policy, rows == 0, $"VerifySql returned {rows} row(s)");
    }

    private static async Task<VerifyResult> EmptyAsync(TablePolicy policy, NpgsqlConnection target, CancellationToken ct) {
        var rows = await ScalarAsync(target, CloneRunner.CountSql(policy with { Where = null }), ct);
        return new VerifyResult(policy.Table, policy.Policy, rows == 0, $"target {rows}");
    }

    private static async Task<VerifyResult> OrphansAsync(
        TablePolicy policy, string column, IReadOnlyCollection<string> userIds, NpgsqlConnection target, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(OrphanSql(policy.Table, column), target);
        cmd.Parameters.AddWithValue(userIds.ToArray());
        var orphans = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        return new VerifyResult(policy.Table, policy.Policy, orphans == 0, $"{column}: {orphans} orphan row(s)");
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection conn, string sql, CancellationToken ct) {
        await using var cmd = new NpgsqlCommand(sql, conn);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }
}
