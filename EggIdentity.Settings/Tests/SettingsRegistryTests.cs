namespace EggIdentity.Settings.Tests;

public class SettingsRegistryTests {
    private static SettingDescriptor Descriptor(string key, string envKey, string category = "Core") =>
        new(key, envKey, key, category, SettingKind.Text, ApplyTier.RestartRequired, Sensitivity.Plain);

    private static FieldDescriptor Field(string name, SettingKind kind = SettingKind.Text) => new(name, name, kind);

    private static CollectionDescriptor Collection(string key, string idField = "name", string? displayField = null, params FieldDescriptor[] fields) =>
        new(key, key, "Core", fields.Length == 0 ? [Field("name"), Field("image")] : fields, idField, displayField);

    [Fact]
    public void Compose_MergesProvidersAcrossAssemblies() {
        var registry = new SettingsRegistry([
            new StaticSettingsProvider([Descriptor("a.one", "A_ONE")]),
            new StaticSettingsProvider([Descriptor("b.two", "B_TWO", "Other")]),
        ]);

        Assert.Equal(2, registry.All.Count);
        Assert.Equal(["Core", "Other"], registry.Categories);
        Assert.Equal("A_ONE", registry.Require("a.one").EnvKey);
    }

    [Fact]
    public void DuplicateKey_ThrowsAtComposition() {
        var ex = Assert.Throws<InvalidOperationException>(() => new SettingsRegistry([
            new StaticSettingsProvider([Descriptor("a.one", "A_ONE")]),
            new StaticSettingsProvider([Descriptor("a.one", "A_ONE_ALT")]),
        ]));

        Assert.Contains("a.one", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Find_ReturnsNullForUnknownKey() {
        var registry = new SettingsRegistry([new StaticSettingsProvider([Descriptor("a.one", "A_ONE")])]);

        Assert.Null(registry.Find("nope"));
        Assert.Throws<KeyNotFoundException>(() => registry.Require("nope"));
    }

    [Fact]
    public void Collections_ComposeAlongsideScalars() {
        var registry = new SettingsRegistry(
            [new StaticSettingsProvider([Descriptor("a.one", "A_ONE")])],
            [new StaticCollectionProvider([Collection("deploy.apps", displayField: "image")])]);

        var only = Assert.Single(registry.Collections);
        Assert.Equal("deploy.apps", only.Key);
        Assert.Same(only, registry.FindCollection("deploy.apps"));
        Assert.Null(registry.FindCollection("nope"));
        Assert.Throws<KeyNotFoundException>(() => registry.RequireCollection("nope"));
    }

    [Fact]
    public void DuplicateCollectionKey_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => new SettingsRegistry([], [
            new StaticCollectionProvider([Collection("deploy.apps")]),
            new StaticCollectionProvider([Collection("deploy.apps")]),
        ]));

        Assert.Contains("deploy.apps", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateFieldName_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => new SettingsRegistry([], [
            new StaticCollectionProvider([Collection("c", fields: [Field("name"), Field("name")])]),
        ]));

        Assert.Contains("name", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IdField_MustBeDeclared() {
        Assert.Throws<InvalidOperationException>(() => new SettingsRegistry([], [
            new StaticCollectionProvider([Collection("c", idField: "missing")]),
        ]));
    }

    [Fact]
    public void DisplayField_MustBeDeclared() {
        Assert.Throws<InvalidOperationException>(() => new SettingsRegistry([], [
            new StaticCollectionProvider([Collection("c", displayField: "missing")]),
        ]));
    }

    [Fact]
    public void Compose_AlwaysIncludesTheFrameworkProvider() {
        var registry = SettingsRegistry.Compose([new StaticSettingsProvider([Descriptor("a.one", "A_ONE")])], []);

        var key = registry.Require(SettingsFrameworkSettings.EncryptionKey);
        Assert.Equal(SettingsFrameworkSettings.EncryptionKeyEnv, key.EnvKey);
        Assert.Equal(ApplyTier.Bootstrap, key.Tier);
        Assert.Equal(2, registry.All.Count);
    }

    [Fact]
    public void Compose_LetsTheFrameworkDescriptorWinOverAConsumerCopy() {
        var copy = new SettingDescriptor(
            SettingsFrameworkSettings.EncryptionKey, "SOMETHING_ELSE", "Copy", "Elsewhere",
            SettingKind.Text, ApplyTier.Live, Sensitivity.Plain);

        var registry = SettingsRegistry.Compose(
            [new StaticSettingsProvider([copy, Descriptor("a.one", "A_ONE")])], []);

        Assert.Equal(SettingsFrameworkSettings.EncryptionKeyEnv, registry.Require(SettingsFrameworkSettings.EncryptionKey).EnvKey);
        Assert.Equal(2, registry.All.Count);
    }

    [Fact]
    public void Compose_StillRejectsOtherDuplicates() {
        Assert.Throws<InvalidOperationException>(() => SettingsRegistry.Compose([
            new StaticSettingsProvider([Descriptor("a.one", "A_ONE")]),
            new StaticSettingsProvider([Descriptor("a.one", "A_ONE_ALT")]),
        ], []));
    }

    [Fact]
    public void External_IsNeverEditable() {
        var external = new SettingDescriptor(
            "capture.iface", "CAPTURE_IFACE", "Capture interface", "Core",
            SettingKind.External, ApplyTier.Live, Sensitivity.Plain);

        Assert.False(external.Editable);
    }

    private sealed class Annotated {
        [Setting("x.flag", "X_FLAG", "Flag", "Core", SettingKind.Bool, Tier = ApplyTier.Live, Default = "false")]
        public bool Flag { get; init; }

        public string Ignored { get; init; } = "";
    }

    [Fact]
    public void AttributeProvider_ReflectsOnlyAnnotatedProperties() {
        var descriptors = new AttributeSettingsProvider<Annotated>().Describe();

        var only = Assert.Single(descriptors);
        Assert.Equal("x.flag", only.Key);
        Assert.Equal("X_FLAG", only.EnvKey);
        Assert.Equal(ApplyTier.Live, only.Tier);
        Assert.Equal("false", only.Default);
    }
}
