using System.Reflection;
using EggIdentity.Db;
using Npgsql;

namespace EggIdentity.Settings.Store.Tests;

public class SettingsStoreTests {
    private static readonly SettingDescriptor Plain =
        new("t.plain", "T_PLAIN", "Plain", "Core", SettingKind.Text, ApplyTier.RestartRequired, Sensitivity.Plain);

    private static readonly SettingDescriptor Secret =
        new("t.secret", "T_SECRET", "Secret", "Core", SettingKind.Secret, ApplyTier.RestartRequired, Sensitivity.Secret);

    private const string MigrationPrefix = "EggIdentity.Settings.Store.Migrations.";

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
}
