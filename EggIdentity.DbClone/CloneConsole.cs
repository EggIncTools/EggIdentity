using System.Globalization;
using Npgsql;

namespace EggIdentity.DbClone;

public sealed record CloneCommand(string Verb, bool Commit);

public static class CloneConsole {
    public const string SourceEnvKey = "CLONE_SOURCE_DB_CONNECTION";
    public const string TargetEnvKey = "DATABASE_URL";
    public const string Doctor = "doctor";
    public const string Plan = "plan";
    public const string Clone = "clone";
    public const string Verify = "verify";
    public const string CommitFlag = "--commit";

    private static readonly string[] Verbs = [Doctor, Plan, Clone, Verify];

    public static string Usage => $"usage: {Doctor} | {Plan} | {Clone} [{CommitFlag}] | {Verify}";

    public static CloneCommand? Parse(IReadOnlyList<string> args, out string? error) {
        ArgumentNullException.ThrowIfNull(args);
        error = null;
        if (args.Count == 0) {
            error = Usage;
            return null;
        }
        var verb = args[0];
        if (!Verbs.Contains(verb, StringComparer.Ordinal)) {
            error = $"unknown verb \"{verb}\". {Usage}";
            return null;
        }
        var commit = false;
        foreach (var arg in args.Skip(1)) {
            if (string.Equals(arg, CommitFlag, StringComparison.Ordinal) && verb == Clone) {
                commit = true;
                continue;
            }
            error = $"unexpected argument \"{arg}\". {Usage}";
            return null;
        }
        return new CloneCommand(verb, commit);
    }

    public static async Task<int> RunAsync(IReadOnlyList<string> args, ClonePlan plan, Func<string, string?> env) {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(env);

        if (Parse(args, out var error) is not { } command) {
            Console.Error.WriteLine($"clone: {error}");
            return 1;
        }

        var target = env(TargetEnvKey);
        var source = env(SourceEnvKey);
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(source)) {
            Console.Error.WriteLine($"clone: {TargetEnvKey} and {SourceEnvKey} are required");
            return 1;
        }

        try {
            return command.Verb switch {
                Doctor => await DoctorAsync(plan, source, target, env),
                Plan => await CloneAsync(plan, source, target, false),
                Clone => await CloneAsync(plan, source, target, command.Commit),
                _ => await VerifyAsync(plan, source, target),
            };
        } catch (Exception e) when (e is InvalidOperationException or NpgsqlException or ArgumentException) {
            Console.Error.WriteLine($"clone: {e.Message}");
            return 1;
        }
    }

    private static async Task<int> DoctorAsync(ClonePlan plan, string source, string target, Func<string, string?> env) {
        var environment = EnvironmentSettings.Resolve(env);
        Console.WriteLine($"clone: {EnvironmentSettings.EnvKey}={environment}");
        var endpoints = CloneGuards.Check(plan, source, target);
        Console.WriteLine($"clone: target {endpoints.TargetDatabase}, source {endpoints.SourceDatabase} (read-only)");

        await using var sourceConn = new NpgsqlConnection(endpoints.SourceConnection);
        await sourceConn.OpenAsync();
        await using var targetConn = new NpgsqlConnection(target);
        await targetConn.OpenAsync();
        Console.WriteLine("clone: both databases reachable");

        var validated = await PlanValidator.ValidateAsync(plan, sourceConn, targetConn, CancellationToken.None);
        foreach (var report in validated.Columns) {
            if (report.SourceOnly.Count > 0) Console.WriteLine($"  {report.Table}: source-only columns {string.Join(", ", report.SourceOnly)}");
            if (report.TargetOnly.Count > 0) Console.WriteLine($"  {report.Table}: target-only columns {string.Join(", ", report.TargetOnly)}");
        }
        var order = LoadOrder.Sort(
            [.. validated.Governed.Select(t => t.Table)], await LoadOrder.ForeignKeysAsync(targetConn, CancellationToken.None));
        Console.WriteLine($"clone: plan covers {validated.Governed.Count} table(s), load order {string.Join(" > ", order)}");
        if (!EnvironmentSettings.IsSubProd(environment))
            Console.WriteLine($"clone: warning, {EnvironmentSettings.EnvKey} is not {EnvironmentSettings.SubProd}; the admin route will refuse");
        return 0;
    }

    private static async Task<int> CloneAsync(ClonePlan plan, string source, string target, bool commit) {
        if (!commit) Console.WriteLine($"clone: dry run, re-run with {CommitFlag} to write");
        var counts = await CloneRunner.RunAsync(plan, source, target, !commit, new ConsoleProgress(), CancellationToken.None);
        foreach (var c in counts)
            Console.WriteLine($"  {c.Table,-40} {c.Policy,-10} source={c.SourceRows} target={c.TargetRows}");
        return 0;
    }

    private static async Task<int> VerifyAsync(ClonePlan plan, string source, string target) {
        var results = await CloneVerifier.VerifyAsync(plan, source, target, null, CancellationToken.None);
        var failed = 0;
        foreach (var r in results) {
            if (!r.Ok) failed++;
            Console.WriteLine($"  {(r.Ok ? "ok  " : "FAIL")} {r.Table,-40} {r.Policy,-10} {r.Detail}");
        }
        Console.WriteLine(failed == 0 ? "clone: verify passed" : $"clone: verify failed for {failed} check(s)");
        return failed == 0 ? 0 : 1;
    }

    private sealed class ConsoleProgress : IProgress<CloneEvent> {
        public void Report(CloneEvent value) {
            var table = value.Table is null ? "" : $" [{value.Table}]";
            Console.WriteLine($"{value.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture)} {value.Phase,-9}{table} {value.Message}");
        }
    }
}
