using EggIdentity.Deploy;
using EggIdentity.Host;
using EggIdentity.Settings;
using EggIdentity.Settings.Api;
using EggIdentity.Settings.Store;
using Npgsql;

namespace EggIdentity.Tools;

internal sealed record SuiteAppMerge(IReadOnlyList<(string Id, Dictionary<string, string?> Values)> Rows, IReadOnlyList<string> Notes);

internal static class SuiteAppImport {
    public const string Verb = "import-suite-apps";
    private const string UpdatedBy = Verb;
    private const string LegacyDeployApps = "deploy.apps";

    private static readonly string[] CarriedDeployFields =
        ["name", "environment", "enabled", "public_url", "repo_url", "brand_slug", "listed", "stack", "container", "auto_deploy"];

    private static readonly string[] DroppedDeployFields = ["image", "repository", "tag", "previous_tag", "deploy_secret"];

    public static SuiteAppMerge Merge(
        IReadOnlyList<CollectionRow> deployApps, IReadOnlyList<CollectionRow> adminTargets, IReadOnlyList<CollectionRow> authentikApps) {
        ArgumentNullException.ThrowIfNull(deployApps);
        ArgumentNullException.ThrowIfNull(adminTargets);
        ArgumentNullException.ThrowIfNull(authentikApps);

        var rows = new Dictionary<string, Dictionary<string, string?>>(StringComparer.OrdinalIgnoreCase);
        var notes = new List<string>();

        foreach (var deploy in deployApps) {
            var name = deploy.Get("name") ?? deploy.Id;
            var values = Row(rows, name);
            foreach (var field in CarriedDeployFields) {
                if (deploy.Get(field) is { Length: > 0 } value) values[field] = value;
            }
            var dropped = DroppedDeployFields.Where(f => !string.IsNullOrEmpty(deploy.Get(f))).ToList();
            if (dropped.Count > 0) notes.Add($"{name}: dropped {string.Join(", ", dropped)}; the running image and the compose file own them now");
        }

        foreach (var target in adminTargets) {
            var name = target.Get("name") ?? target.Id;
            if (!rows.ContainsKey(name)) notes.Add($"{name}: admin target with no deploy row, added as its own app");
            var values = Row(rows, name);
            values["admin_base_url"] = target.Get("admin_base_url");
            if (target.Get("enabled") is "false") notes.Add($"{name}: admin target was disabled; the app row stays enabled");
        }

        foreach (var auth in authentikApps) {
            var origin = auth.Get("origin") ?? auth.Id;
            var name = rows.FirstOrDefault(r => SameOrigin(r.Value.GetValueOrDefault("public_url"), origin)).Key;
            if (name is null) {
                name = NameFromOrigin(origin);
                notes.Add($"{name}: login registration for {origin} matched no app's public URL, added as its own app");
            }
            var values = Row(rows, name);
            if (string.IsNullOrEmpty(values.GetValueOrDefault("public_url"))) values["public_url"] = origin;
            values["auth_client_id"] = auth.Get("client_id");
            values["auth_client_secret"] = auth.Get("client_secret");
            values["auth_callback_url"] = auth.Get("callback_url");
            values["auth_end_session_url"] = auth.Get("end_session_url");
        }

        var result = new List<(string, Dictionary<string, string?>)>();
        foreach (var (name, values) in rows.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase)) {
            if (SettingsValidation.ValidateRow(SuiteApps.Descriptor, values) is { } error) {
                notes.Add($"{name}: skipped, {error}");
                continue;
            }
            result.Add((name, values));
        }
        return new SuiteAppMerge(result, notes);
    }

    public static async Task<int> RunAsync(string[] args, CancellationToken ct) {
        var commit = args.Contains("--commit");
        var connString = Program.RequireEnv("IDENTITY_DB_CONNECTION");
        if (connString is null) return 1;
        var protector = SecretProtector.FromEnvironment();
        if (protector is null) {
            Console.Error.WriteLine($"{Verb}: EGGIDENTITY_SETTINGS_KEY is required to read and store client secrets");
            return 1;
        }

        await using var dataSource = NpgsqlDataSource.Create(connString);
        var store = new SettingsStore(dataSource, protector);
        await store.MigrateAsync(ct);

        var legacyAuth = (await store.ListRowsAsync(AuthentikApps.Key, ct)).ToList();
        if (Option(args, "--authentik-dir") is { } dir) legacyAuth.AddRange(ReadAuthentikDir(dir));

        var merge = Merge(
            await store.ListRowsAsync(LegacyDeployApps, ct),
            await store.ListRowsAsync(AdminTargets.LegacyKey, ct),
            legacyAuth);

        foreach (var note in merge.Notes) Console.WriteLine($"{Verb}: note: {note}");
        foreach (var (id, values) in merge.Rows) {
            var shown = values.Where(v => !string.IsNullOrEmpty(v.Value))
                .Select(v => SuiteApps.Descriptor.FindField(v.Key)?.IsSecret == true ? $"{v.Key}=********" : $"{v.Key}={v.Value}");
            Console.WriteLine($"{Verb}: {id}: {string.Join(" ", shown)}");
        }

        if (!commit) {
            Console.WriteLine($"{Verb}: dry run, {merge.Rows.Count} row(s) not written. Re-run with --commit to write them to {SuiteApps.Key}.");
            return 0;
        }
        foreach (var (id, values) in merge.Rows) await store.UpsertRowAsync(SuiteApps.Descriptor, id, values, UpdatedBy, ct);
        Console.WriteLine($"{Verb}: {merge.Rows.Count} row(s) written to {SuiteApps.Key}. The legacy rows were left in place.");
        return 0;
    }

    internal static string NameFromOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var url) ? url.Host.Split('.')[0] : origin;

    private static bool SameOrigin(string? publicUrl, string origin) =>
        Uri.TryCreate(publicUrl, UriKind.Absolute, out var a) && Uri.TryCreate(origin, UriKind.Absolute, out var b)
        && string.Equals(a.GetLeftPart(UriPartial.Authority), b.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string?> Row(Dictionary<string, Dictionary<string, string?>> rows, string name) {
        if (rows.TryGetValue(name, out var existing)) return existing;
        var created = new Dictionary<string, string?>(StringComparer.Ordinal) { ["name"] = name };
        rows[name] = created;
        return created;
    }

    private static string? Option(string[] args, string name) {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }

    private static IEnumerable<CollectionRow> ReadAuthentikDir(string dir) =>
        Directory.EnumerateFiles(dir).Order(StringComparer.Ordinal).Select(path => {
            var file = AppAuthConfigLoader.ParseFile(path);
            var values = new Dictionary<string, string?>(StringComparer.Ordinal) {
                ["origin"] = file.GetValueOrDefault("Origin"),
                ["client_id"] = file.GetValueOrDefault("ClientId"),
                ["client_secret"] = file.GetValueOrDefault("ClientSecret"),
                ["callback_url"] = file.GetValueOrDefault("CallbackUrl"),
                ["end_session_url"] = file.GetValueOrDefault("EndSessionUrl"),
            };
            return new CollectionRow(AuthentikApps.Key, values["origin"] ?? path, values, DateTimeOffset.UtcNow, null);
        });
}
