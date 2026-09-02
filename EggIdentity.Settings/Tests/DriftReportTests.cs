namespace EggIdentity.Settings.Tests;

public class DriftReportTests {
    private static SettingsRegistry Registry(params SettingDescriptor[] descriptors) =>
        new([new StaticSettingsProvider(descriptors)]);

    private static SettingDescriptor Of(string envKey, bool required = false, string? @default = null) =>
        new(envKey.ToLowerInvariant(), envKey, envKey, "Core", SettingKind.Text, ApplyTier.RestartRequired, Sensitivity.Plain) {
            Required = required,
            Default = @default,
        };

    [Fact]
    public void StackKeyWithNoReader_IsSetButUnread() {
        var report = DriftReport.Compare(Registry(Of("READ_ME")), ["READ_ME", "ADMIN_DISCORD_IDS"]);

        Assert.Equal(["ADMIN_DISCORD_IDS"], report.SetButUnread);
        Assert.Equal(["READ_ME"], report.Matched);
        Assert.False(report.IsClean);
    }

    [Fact]
    public void RequiredKeyMissingFromStack_IsReadButUnset() {
        var report = DriftReport.Compare(Registry(Of("NEEDED", required: true)), []);

        Assert.Equal(["NEEDED"], report.ReadButUnset);
    }

    [Fact]
    public void RequiredKeyWithDefault_IsNotDrift() {
        var report = DriftReport.Compare(Registry(Of("NEEDED", required: true, @default: "8090")), []);

        Assert.Empty(report.ReadButUnset);
        Assert.True(report.IsClean);
    }

    [Fact]
    public void OptionalKeyMissingFromStack_IsNotDrift() {
        var report = DriftReport.Compare(Registry(Of("OPTIONAL")), []);

        Assert.True(report.IsClean);
    }
}
