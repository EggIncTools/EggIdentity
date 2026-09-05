using EggIdentity.Host;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;
using Npgsql;

namespace EggIdentity.Tools;

internal static class AuthentikAppImport {
    private const string UpdatedBy = "import-authentik-apps";

    private static readonly (string File, string Field)[] FieldMap = [
        ("Origin", "origin"),
        ("ClientId", "client_id"),
        ("ClientSecret", "client_secret"),
        ("CallbackUrl", "callback_url"),
        ("EndSessionUrl", "end_session_url"),
    ];

    public static (string Id, Dictionary<string, string?> Values) ToRow(IReadOnlyDictionary<string, string> fileValues) {
        ArgumentNullException.ThrowIfNull(fileValues);
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (fileKey, field) in FieldMap)
            values[field] = fileValues.TryGetValue(fileKey, out var v) && v.Length > 0 ? v : null;

        var unknown = fileValues.Keys.Where(k => !FieldMap.Any(m => m.File == k)).ToList();
        if (unknown.Count > 0)
            throw new InvalidOperationException($"unknown key(s): {string.Join(", ", unknown)}");

        if (SettingsValidation.ValidateRow(AuthentikApps.Descriptor, values) is string error)
            throw new InvalidOperationException(error);

        return (values["origin"]!, values);
    }

    public static async Task<int> RunAsync(string dir, CancellationToken ct) {
        if (!Directory.Exists(dir)) {
            Console.Error.WriteLine($"import-authentik-apps: directory not found: {dir}");
            return 1;
        }
        var connString = Program.RequireEnv("IDENTITY_DB_CONNECTION");
        if (connString is null) return 1;
        var protector = SecretProtector.FromEnvironment();
        if (protector is null) {
            Console.Error.WriteLine("import-authentik-apps: EGGIDENTITY_SETTINGS_KEY is required to store client secrets");
            return 1;
        }

        var rows = new List<(string Id, Dictionary<string, string?> Values)>();
        foreach (var path in Directory.EnumerateFiles(dir).Order(StringComparer.Ordinal)) {
            try {
                rows.Add(ToRow(AppAuthConfigLoader.ParseFile(path)));
            } catch (InvalidOperationException e) {
                Console.Error.WriteLine($"import-authentik-apps: {Path.GetFileName(path)}: {e.Message}");
                return 1;
            }
        }

        await using var dataSource = NpgsqlDataSource.Create(connString);
        var store = new SettingsStore(dataSource, protector);
        await store.MigrateAsync(ct);
        foreach (var (id, values) in rows) {
            await store.UpsertRowAsync(AuthentikApps.Descriptor, id, values, UpdatedBy, ct);
            Console.WriteLine($"import-authentik-apps: upserted {id}");
        }
        Console.WriteLine($"import-authentik-apps: {rows.Count} row(s) written to {AuthentikApps.Key}. Unset AUTHENTIK_APPS_DIR and drop the mount.");
        return 0;
    }
}
