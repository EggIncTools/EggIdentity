namespace EggIdentity.Settings.Tests;

public class SettingsSnapshotTests {
    private static SettingsRegistry Registry(params SettingDescriptor[] descriptors) =>
        new([new StaticSettingsProvider(descriptors)]);

    private static SettingDescriptor Plain(string key, string envKey, string? @default = null, ApplyTier tier = ApplyTier.RestartRequired) =>
        new(key, envKey, key, "Core", SettingKind.Text, tier, Sensitivity.Plain) { Default = @default };

    [Fact]
    public void Database_WinsOverFileEnvAndDefault() {
        var registry = Registry(Plain("a", "A", "fallback"));
        var snapshot = new SettingsSnapshot(
            registry,
            new Dictionary<string, string?> { ["a"] = "from-db" },
            _ => "from-file",
            _ => "from-env");

        var value = snapshot.Value("a");
        Assert.Equal("from-db", value.Value);
        Assert.Equal(SettingSource.Database, value.Source);
    }

    [Fact]
    public void File_WinsOverEnvAndDefault() {
        var registry = Registry(Plain("a", "A", "fallback"));
        var snapshot = new SettingsSnapshot(registry, new Dictionary<string, string?>(), _ => "from-file", _ => "from-env");

        Assert.Equal(SettingSource.File, snapshot.Value("a").Source);
    }

    [Fact]
    public void Env_WinsOverDefault() {
        var registry = Registry(Plain("a", "A", "fallback"));
        var snapshot = new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => "from-env");

        Assert.Equal(SettingSource.Environment, snapshot.Value("a").Source);
    }

    [Fact]
    public void Default_IsLastResort() {
        var registry = Registry(Plain("a", "A", "fallback"));
        var snapshot = new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null);

        var value = snapshot.Value("a");
        Assert.Equal("fallback", value.Value);
        Assert.Equal(SettingSource.Default, value.Source);
    }

    [Fact]
    public void EmptyDatabaseValue_DoesNotWin() {
        var registry = Registry(Plain("a", "A"));
        var snapshot = new SettingsSnapshot(
            registry, new Dictionary<string, string?> { ["a"] = "" }, null, _ => "from-env");

        Assert.Equal(SettingSource.Environment, snapshot.Value("a").Source);
    }

    [Fact]
    public void BootstrapTier_IgnoresDatabase() {
        var registry = Registry(Plain("a", "A", tier: ApplyTier.Bootstrap));
        var snapshot = new SettingsSnapshot(
            registry, new Dictionary<string, string?> { ["a"] = "from-db" }, null, _ => "from-env");

        var value = snapshot.Value("a");
        Assert.Equal("from-env", value.Value);
        Assert.Equal(SettingSource.Environment, value.Source);
    }

    [Fact]
    public void SecretValue_IsMaskedForDisplayButReadableForUse() {
        var registry = Registry(new SettingDescriptor(
            "s", "S", "Secret", "Core", SettingKind.Secret, ApplyTier.RestartRequired, Sensitivity.Secret));
        var snapshot = new SettingsSnapshot(registry, new Dictionary<string, string?> { ["s"] = "hunter2" });

        var value = snapshot.Value("s");
        Assert.Equal("hunter2", value.Value);
        Assert.Equal("********", value.Display);
    }

    [Fact]
    public void UnknownKey_Throws() {
        var snapshot = new SettingsSnapshot(Registry(Plain("a", "A")), new Dictionary<string, string?>());

        Assert.Throws<KeyNotFoundException>(() => snapshot.Value("nope"));
    }
}
