namespace EggIdentity.Settings.Tests;

public class SettingsRegistryTests {
    private static SettingDescriptor Descriptor(string key, string envKey, string category = "Core") =>
        new(key, envKey, key, category, SettingKind.Text, ApplyTier.RestartRequired, Sensitivity.Plain);

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
