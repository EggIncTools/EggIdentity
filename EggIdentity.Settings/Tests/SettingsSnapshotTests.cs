namespace EggIdentity.Settings.Tests;

public class SettingsSnapshotTests {
    private static SettingsRegistry Registry(params SettingDescriptor[] descriptors) =>
        new([new StaticSettingsProvider(descriptors)]);

    private static SettingDescriptor Plain(string key, string envKey, string? @default = null, ApplyTier tier = ApplyTier.RestartRequired) =>
        new(key, envKey, key, "Core", SettingKind.Text, tier, Sensitivity.Plain) { Default = @default };

    private static readonly CollectionDescriptor Apps = new(
        "deploy.apps", "Apps", "Deploy",
        [
            new FieldDescriptor("name", "Name", SettingKind.Text) { Required = true },
            new FieldDescriptor("image", "Image", SettingKind.Text) { Required = true },
            new FieldDescriptor("auto_deploy", "Auto deploy", SettingKind.Bool) { Default = "true" },
        ],
        "name");

    private sealed record DeployApp(string Name, string Image, bool AutoDeploy);

    private static CollectionRow Row(string id, params (string Field, string? Value)[] pairs) =>
        new("deploy.apps", id, pairs.ToDictionary(p => p.Field, p => p.Value, StringComparer.Ordinal), DateTimeOffset.UnixEpoch, null);

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

    [Fact]
    public void Collections_BindRowsToTypedRecords() {
        var registry = new SettingsRegistry([], [new StaticCollectionProvider([Apps])]);
        var snapshot = new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null,
            new Dictionary<string, IReadOnlyList<CollectionRow>> {
                ["deploy.apps"] = [
                    Row("eggledger", ("name", "eggledger"), ("image", "ghcr.io/x/ledger"), ("auto_deploy", "false")),
                    Row("eggincognito", ("name", "eggincognito"), ("image", "ghcr.io/x/incognito")),
                ],
            });

        var apps = snapshot.Collection<DeployApp>("deploy.apps");

        Assert.Equal(2, apps.Count);
        Assert.False(apps[0].AutoDeploy);
        Assert.True(apps[1].AutoDeploy);
        Assert.Equal("ghcr.io/x/incognito", apps[1].Image);
    }

    [Fact]
    public void Collections_FillDeclaredDefaultsAndDropUndeclaredFields() {
        var registry = new SettingsRegistry([], [new StaticCollectionProvider([Apps])]);
        var snapshot = new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null,
            new Dictionary<string, IReadOnlyList<CollectionRow>> {
                ["deploy.apps"] = [Row("a", ("name", "a"), ("image", "b"), ("stale", "x"))],
            });

        var row = Assert.Single(snapshot.Rows("deploy.apps"));
        Assert.Equal("true", row.Get("auto_deploy"));
        Assert.False(row.Values.ContainsKey("stale"));
    }

    [Fact]
    public void Collections_DeclaredButUnstored_IsEmptyNotMissing() {
        var registry = new SettingsRegistry([], [new StaticCollectionProvider([Apps])]);
        var snapshot = new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null);

        Assert.Empty(snapshot.Rows("deploy.apps"));
        Assert.Empty(snapshot.Collection<DeployApp>("deploy.apps"));
    }

    [Fact]
    public void Collections_UndeclaredKey_Throws() {
        var snapshot = new SettingsSnapshot(Registry(), new Dictionary<string, string?>(), null, _ => null,
            new Dictionary<string, IReadOnlyList<CollectionRow>> { ["other.app"] = [Row("x", ("name", "x"))] });

        Assert.Throws<KeyNotFoundException>(() => snapshot.Rows("other.app"));
    }
}
