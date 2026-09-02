using EggIdentity.Db;
using Npgsql;

namespace EggIdentity.Tools;

internal static class Program {
    private static async Task<int> Main(string[] args) {
        var egiConn = RequireEnv("EGI_SOURCE_DB_CONNECTION");
        var ledgerConn = RequireEnv("LEDGER_SOURCE_DB_CONNECTION");
        var targetConn = RequireEnv("IDENTITY_DB_CONNECTION");
        if (egiConn is null || ledgerConn is null || targetConn is null) return 1;

        var merge = await ReadAndMergeAsync(egiConn, ledgerConn);
        Report(merge);

        if (!args.Contains("--commit")) {
            Console.WriteLine("cutover: dry run only, no writes made. Re-run with --commit to apply.");
            return 0;
        }

        Console.WriteLine("cutover: applying migrations to target...");
        await ApplyMigrationsAsync(targetConn);

        Console.WriteLine("cutover: writing merged users/identities to target...");
        await CutoverWriter.WriteAsync(targetConn, merge, CancellationToken.None);
        Console.WriteLine("cutover: done. Source databases were not modified.");
        return 0;
    }

    private static async Task<MergeResult> ReadAndMergeAsync(string egiConn, string ledgerConn) {
        Console.WriteLine("cutover: reading source databases (read-only)...");
        var egi = await SourceReader.ReadEggIncognitoAsync(egiConn, CancellationToken.None);
        var ledger = await SourceReader.ReadEggLedgerAsync(ledgerConn, CancellationToken.None);
        Console.WriteLine($"cutover: eggincognito users={egi.Users.Count} identities={egi.Identities.Count}");
        Console.WriteLine($"cutover: eggledger    users={ledger.Users.Count} identities={ledger.Identities.Count}");
        return CutoverMerger.Merge(egi, ledger);
    }

    private static void Report(MergeResult merge) {
        Console.WriteLine($"cutover: {merge.Remaps.Count} discord_id collisions found (same person, both apps):");
        foreach (var r in merge.Remaps)
            Console.WriteLine($"  discord_id={r.DiscordId} keep={r.KeptUserId} ({r.SourceOfKept}) retire={r.RetiredUserId}");

        Console.WriteLine($"cutover: merged result -> {merge.Users.Count} users, {merge.Identities.Count} identities");

        if (merge.Orphans.Count == 0) return;

        Console.WriteLine($"cutover: {merge.Orphans.Count} orphaned identity row(s) skipped (user_id has no matching users row, pre-existing source data issue):");
        foreach (var o in merge.Orphans)
            Console.WriteLine($"  source={o.Source} user_id={o.UserId} provider={o.Provider} subject={o.Subject}");
    }

    private static async Task ApplyMigrationsAsync(string targetConn) {
        await using var targetDb = NpgsqlDataSource.Create(targetConn);
        await using var conn = await targetDb.OpenConnectionAsync();
        await Migrator.MigrateAsync(conn, Path.Combine(AppContext.BaseDirectory, "Migrations"));
    }

    private static string? RequireEnv(string name) {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value)) {
            Console.Error.WriteLine($"cutover: {name} is required");
            return null;
        }
        return value;
    }
}
