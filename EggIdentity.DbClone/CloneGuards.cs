using Npgsql;

namespace EggIdentity.DbClone;

public sealed record CloneEndpoints(string SourceDatabase, string TargetDatabase, string SourceConnection);

public static class CloneGuards {
    public const string ReadOnlyOption = "-c default_transaction_read_only=on";

    public static string ReadOnlySource(string sourceConn) {
        SubProdGuard.DatabaseName(sourceConn);
        var builder = new NpgsqlConnectionStringBuilder(sourceConn);
        builder.Options = string.IsNullOrWhiteSpace(builder.Options)
            ? ReadOnlyOption
            : builder.Options + " " + ReadOnlyOption;
        return builder.ConnectionString;
    }

    public static CloneEndpoints Check(ClonePlan plan, string sourceConn, string targetConn) {
        ArgumentNullException.ThrowIfNull(plan);
        var target = SubProdGuard.EnsureDatabase(targetConn, plan.TargetDatabase);
        var source = SubProdGuard.DatabaseName(sourceConn);
        if (string.Equals(source, target, StringComparison.Ordinal))
            throw new InvalidOperationException($"source and target are the same database \"{target}\"");
        return new CloneEndpoints(source, target, ReadOnlySource(sourceConn));
    }
}
