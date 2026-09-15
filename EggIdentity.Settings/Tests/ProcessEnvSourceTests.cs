namespace EggIdentity.Settings.Tests;

public class ProcessEnvSourceTests {
    private sealed class Provider(params SettingDescriptor[] descriptors) : ISettingsProvider {
        public IReadOnlyList<SettingDescriptor> Describe() => descriptors;
    }

    private static SettingsRegistry Registry(params SettingDescriptor[] descriptors) =>
        new([new Provider(descriptors)]);

    private static SettingDescriptor Declared(string key, string envKey) =>
        new(key, envKey, envKey, "Test", SettingKind.Text, ApplyTier.Bootstrap, Sensitivity.Plain);

    [Fact]
    public void ADeclaredKeyThatIsSet_IsReported() {
        var name = "EGGIDENTITY_TEST_DECLARED_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Environment.SetEnvironmentVariable(name, "value");
        try {
            var keys = ProcessEnvSource.Read(Registry(Declared("a.one", name)));
            Assert.Contains(keys, k => k.Name == name && k.Origin == EnvOrigin.ServiceEnvironment);
        } finally {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void ADeclaredKeyThatIsNotSet_IsAbsent_SoDriftCallsItMissing() {
        var name = "EGGIDENTITY_TEST_ABSENT_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var keys = ProcessEnvSource.Read(Registry(Declared("a.one", name)));
        Assert.DoesNotContain(keys, k => k.Name == name);
    }

    [Fact]
    public void AnUndeclaredAppShapedKey_IsReported_SoDriftCanCallItUndeclared() {
        var name = "EGGIDENTITY_TEST_STRAY_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Environment.SetEnvironmentVariable(name, "value");
        try {
            Assert.Contains(ProcessEnvSource.Read(Registry()), k => k.Name == name);
        } finally {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void WellKnownHostVariables_AreNotReportedAsDrift() {
        var keys = ProcessEnvSource.Read(Registry());
        Assert.DoesNotContain(keys, k => k.Name == "PATH");
        Assert.DoesNotContain(keys, k => k.Name == "TEMP");
    }

    [Fact]
    public void ValuesAreNeverCarried() {
        var name = "EGGIDENTITY_TEST_SECRET_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Environment.SetEnvironmentVariable(name, "hunter2");
        try {
            var entry = ProcessEnvSource.Read(Registry()).Single(k => k.Name == name);
            Assert.Null(entry.Value);
            Assert.True(entry.Masked);
        } finally {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public async Task TheInstanceSourceReturnsWhatTheStaticReaderDoes() {
        var registry = Registry();
        var source = new ProcessEnvSource(registry);
        var fromInstance = await source.GetAsync(CancellationToken.None);
        Assert.Equal(ProcessEnvSource.Read(registry).Select(k => k.Name), fromInstance.Select(k => k.Name));
    }
}
