using System.Reflection;
using System.Security.Cryptography;
using EggIdentity.Db;
using Npgsql;

namespace EggIdentity.Settings.Store.Tests;

public class SettingsStoreTests {
    private static readonly SettingDescriptor Plain =
        new("t.plain", "T_PLAIN", "Plain", "Core", SettingKind.Text, ApplyTier.RestartRequired, Sensitivity.Plain);

    private static readonly SettingDescriptor Secret =
        new("t.secret", "T_SECRET", "Secret", "Core", SettingKind.Secret, ApplyTier.RestartRequired, Sensitivity.Secret);

    private static readonly CollectionDescriptor Apps = new(
        "t.apps", "Apps", "Core",
        [
            new FieldDescriptor("name", "Name", SettingKind.Text) { Required = true },
            new FieldDescriptor("image", "Image", SettingKind.Text),
            new FieldDescriptor("deploy_secret", "Deploy secret", SettingKind.Secret, Sensitivity.Secret),
        ],
        "name");

    private const string MigrationPrefix = "EggIdentity.Settings.Store.Migrations.";
    private const string LedgerId = "ledger";
    private const string IncognitoId = "incognito";

    [Fact]
    public void EmbeddedMigrations_AreShippedInsideThePackage() {
        var names = Migrator.EmbeddedMigrations(typeof(SettingsStore).Assembly, MigrationPrefix);

        Assert.NotEmpty(names);
        Assert.Contains(names, n => n.EndsWith("1_app_settings.up.sql", StringComparison.Ordinal));
    }

    [Fact]
    public void EmbeddedMigrations_AreOrderedNumerically() {
        var names = Migrator.EmbeddedMigrations(typeof(SettingsStore).Assembly, MigrationPrefix);

        var versions = names.Select(n => Migrator.PrefixNum(n[MigrationPrefix.Length..])).ToList();

        Assert.Equal(versions.Order(), versions);
        Assert.Equal(versions.Distinct(), versions);
    }

    [Fact]
    public void AddingUpdatedBy_ShipsAsAnAlterNotAFreshCreate() {
        var names = Migrator.EmbeddedMigrations(typeof(SettingsStore).Assembly, MigrationPrefix);
        var updatedBy = Assert.Single(names, n => n.Contains("updated_by", StringComparison.Ordinal));

        using var stream = typeof(SettingsStore).Assembly.GetManifestResourceStream(updatedBy)!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        Assert.Contains("ALTER TABLE app_settings", sql, StringComparison.Ordinal);
        Assert.Contains("ADD COLUMN IF NOT EXISTS updated_by", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionsTable_ShipsAsMigrationThree() {
        var names = Migrator.EmbeddedMigrations(typeof(SettingsStore).Assembly, MigrationPrefix);
        var third = Assert.Single(names, n => Migrator.PrefixNum(n[MigrationPrefix.Length..]) == 3);

        Assert.EndsWith("3_app_setting_collections.up.sql", third, StringComparison.Ordinal);

        using var stream = typeof(SettingsStore).Assembly.GetManifestResourceStream(third)!;
        using var reader = new StreamReader(stream);
        var sql = reader.ReadToEnd();

        Assert.Contains("CREATE TABLE IF NOT EXISTS app_setting_collections", sql, StringComparison.Ordinal);
        Assert.Contains("value JSONB NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("PRIMARY KEY (collection, id)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void EmbeddedMigrations_IgnoreNonMatchingPrefixes() {
        var assembly = Assembly.GetExecutingAssembly();

        Assert.Empty(Migrator.EmbeddedMigrations(assembly, "nothing.matches."));
    }

    [Fact]
    public async Task SecretWithoutKey_CannotBeStored() {
        await using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=unused");
        var store = new SettingsStore(dataSource);

        Assert.False(store.CanStoreSecrets);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SetAsync(Secret, "value", null, CancellationToken.None));
    }

    [Fact]
    public async Task RowWithSecretFieldWithoutKey_CannotBeStored() {
        await using var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=unused");
        var store = new SettingsStore(dataSource);
        var values = new Dictionary<string, string?> { ["name"] = LedgerId };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.UpsertRowAsync(Apps, LedgerId, values, null, CancellationToken.None));
    }

    [Fact]
    public async Task RoundTrip_PersistsPlainAndEncryptsSecrets() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrEmpty(conn)) return;

        await using var dataSource = NpgsqlDataSource.Create(conn);
        var protector = SecretProtector.FromKey(Convert.ToBase64String(new byte[32]));
        var store = new SettingsStore(dataSource, protector);
        await store.MigrateAsync(CancellationToken.None);

        await store.SetAsync(Plain, "visible", "tester", CancellationToken.None);
        await store.SetAsync(Secret, "hunter2", "tester", CancellationToken.None);

        var all = await store.GetAllAsync(CancellationToken.None);
        Assert.Equal("visible", all[Plain.Key]);
        Assert.Equal("hunter2", all[Secret.Key]);

        await using (var raw = await dataSource.OpenConnectionAsync())
        await using (var cmd = new NpgsqlCommand("SELECT value FROM app_settings WHERE key = $1", raw)) {
            cmd.Parameters.AddWithValue(Secret.Key);
            var stored = (string?)await cmd.ExecuteScalarAsync();
            Assert.True(SecretProtector.IsProtected(stored));
        }

        await store.DeleteAsync(Plain.Key, CancellationToken.None);
        await store.DeleteAsync(Secret.Key, CancellationToken.None);
    }

    [Fact]
    public async Task RowRoundTrip_PersistsFieldsAndEncryptsSecretFields() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrEmpty(conn)) return;

        await using var dataSource = NpgsqlDataSource.Create(conn);
        var protector = SecretProtector.FromKey(Convert.ToBase64String(new byte[32]));
        var store = new SettingsStore(dataSource, protector);
        await store.MigrateAsync(CancellationToken.None);

        var ledger = new Dictionary<string, string?> {
            ["name"] = LedgerId,
            ["image"] = "ghcr.io/x/ledger:latest",
            ["deploy_secret"] = "hunter2",
        };
        var incognito = new Dictionary<string, string?> { ["name"] = IncognitoId };
        await store.UpsertRowAsync(Apps, LedgerId, ledger, "tester", CancellationToken.None);
        await store.UpsertRowAsync(Apps, IncognitoId, incognito, "tester", CancellationToken.None);

        var row = await store.GetRowAsync(Apps.Key, LedgerId, CancellationToken.None);
        Assert.NotNull(row);
        Assert.Equal("ghcr.io/x/ledger:latest", row.Get("image"));
        Assert.Equal("hunter2", row.Get("deploy_secret"));
        Assert.Equal("tester", row.UpdatedBy);

        var listed = await store.ListRowsAsync(Apps.Key, CancellationToken.None);
        Assert.Equal([IncognitoId, LedgerId], listed.Select(r => r.Id));

        var all = await store.GetAllRowsAsync(CancellationToken.None);
        Assert.Equal(2, all[Apps.Key].Count);

        await using (var raw = await dataSource.OpenConnectionAsync())
        await using (var cmd = new NpgsqlCommand(
            "SELECT value->>'deploy_secret' FROM app_setting_collections WHERE collection = $1 AND id = $2", raw)) {
            cmd.Parameters.AddWithValue(Apps.Key);
            cmd.Parameters.AddWithValue(LedgerId);
            var stored = (string?)await cmd.ExecuteScalarAsync();
            Assert.True(SecretProtector.IsProtected(stored));
        }

        await store.DeleteRowAsync(Apps.Key, LedgerId, CancellationToken.None);
        await store.DeleteRowAsync(Apps.Key, IncognitoId, CancellationToken.None);
        Assert.Null(await store.GetRowAsync(Apps.Key, LedgerId, CancellationToken.None));
        Assert.Empty(await store.ListRowsAsync(Apps.Key, CancellationToken.None));
    }

    [Fact]
    public async Task StoredSecret_ReadWithoutKeyOrWithRotatedKey_FailsClosed() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrEmpty(conn)) return;

        await using var dataSource = NpgsqlDataSource.Create(conn);
        var writer = new SettingsStore(dataSource, SecretProtector.FromKey(RandomKey()));
        await writer.MigrateAsync(CancellationToken.None);
        await writer.SetAsync(Secret, "hunter2", "tester", CancellationToken.None);

        try {
            var blind = new SettingsStore(dataSource);
            var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => blind.GetAllAsync(CancellationToken.None));
            Assert.Contains(Secret.Key, missing.Message, StringComparison.Ordinal);
            Assert.Contains("EGGIDENTITY_SETTINGS_KEY is not configured", missing.Message, StringComparison.Ordinal);
            await Assert.ThrowsAsync<InvalidOperationException>(() => blind.GetAsync(Secret.Key, CancellationToken.None));

            var rotated = new SettingsStore(dataSource, SecretProtector.FromKey(RandomKey()));
            var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() => rotated.GetAsync(Secret.Key, CancellationToken.None));
            Assert.Contains("does not match the key it was written with", mismatch.Message, StringComparison.Ordinal);
            await Assert.ThrowsAsync<InvalidOperationException>(() => rotated.GetAllAsync(CancellationToken.None));
        } finally {
            await writer.DeleteAsync(Secret.Key, CancellationToken.None);
        }
    }

    [Fact]
    public async Task StoredSecretField_ReadWithoutKeyOrWithRotatedKey_FailsClosedNamingTheField() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrEmpty(conn)) return;

        await using var dataSource = NpgsqlDataSource.Create(conn);
        var writer = new SettingsStore(dataSource, SecretProtector.FromKey(RandomKey()));
        await writer.MigrateAsync(CancellationToken.None);
        var values = new Dictionary<string, string?> { ["name"] = LedgerId, ["deploy_secret"] = "hunter2" };
        await writer.UpsertRowAsync(Apps, LedgerId, values, "tester", CancellationToken.None);

        try {
            var blind = new SettingsStore(dataSource);
            var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => blind.GetRowAsync(Apps.Key, LedgerId, CancellationToken.None));
            Assert.Contains("deploy_secret", missing.Message, StringComparison.Ordinal);
            Assert.Contains("EGGIDENTITY_SETTINGS_KEY is not configured", missing.Message, StringComparison.Ordinal);
            await Assert.ThrowsAsync<InvalidOperationException>(() => blind.ListRowsAsync(Apps.Key, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => blind.GetAllRowsAsync(CancellationToken.None));

            var rotated = new SettingsStore(dataSource, SecretProtector.FromKey(RandomKey()));
            var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() => rotated.GetRowAsync(Apps.Key, LedgerId, CancellationToken.None));
            Assert.Contains("deploy_secret", mismatch.Message, StringComparison.Ordinal);
            Assert.Contains("does not match the key it was written with", mismatch.Message, StringComparison.Ordinal);
        } finally {
            await writer.DeleteRowAsync(Apps.Key, LedgerId, CancellationToken.None);
        }
    }

    [Fact]
    public async Task CreateRow_WithExistingId_IsRejectedAndKeepsTheStoredRow() {
        var conn = Environment.GetEnvironmentVariable("EGGIDENTITY_TEST_PG_CONN");
        if (string.IsNullOrEmpty(conn)) return;

        await using var dataSource = NpgsqlDataSource.Create(conn);
        var store = new SettingsStore(dataSource, SecretProtector.FromKey(RandomKey()));
        await store.MigrateAsync(CancellationToken.None);
        var registry = new SettingsRegistry([], [new StaticCollectionProvider([Apps])]);
        using var cache = new SettingsCache(registry, store);
        var admin = new SettingsAdminService(registry, store, cache);

        try {
            var first = new Dictionary<string, string?> { ["image"] = "ghcr.io/x/ledger:1", ["deploy_secret"] = "hunter2" };
            var created = await admin.CreateRowAsync(Apps.Key, LedgerId, first, "tester", CancellationToken.None);
            Assert.True(created.Ok);

            var duplicate = new Dictionary<string, string?> { ["image"] = "ghcr.io/x/ledger:2", ["deploy_secret"] = "" };
            var rejected = await admin.CreateRowAsync(Apps.Key, LedgerId, duplicate, "tester", CancellationToken.None);
            Assert.False(rejected.Ok);
            Assert.Equal($"Apps \"{LedgerId}\" already exists", rejected.Error);

            var row = await store.GetRowAsync(Apps.Key, LedgerId, CancellationToken.None);
            Assert.NotNull(row);
            Assert.Equal("ghcr.io/x/ledger:1", row.Get("image"));
            Assert.Equal("hunter2", row.Get("deploy_secret"));
        } finally {
            await store.DeleteRowAsync(Apps.Key, LedgerId, CancellationToken.None);
        }
    }

    private static string RandomKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
