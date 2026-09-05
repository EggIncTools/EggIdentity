namespace EggIdentity.Settings.Tests;

public class DriftReportTests {
    private static SettingsRegistry Registry(params SettingDescriptor[] descriptors) =>
        new([new StaticSettingsProvider(descriptors)]);

    private static SettingDescriptor Of(
        string envKey, bool required = false, string? @default = null,
        SettingKind kind = SettingKind.Text, ApplyTier tier = ApplyTier.RestartRequired) =>
        new(envKey.ToLowerInvariant(), envKey, envKey, "Core", kind, tier, Sensitivity.Plain) {
            Required = required,
            Default = @default,
        };

    private static EnvKeyInfo Env(string name, EnvOrigin origin = EnvOrigin.ServiceEnvironment, bool referenced = true) =>
        new(name, origin) { Referenced = referenced };

    [Fact]
    public void ServiceKeyWithNoDescriptor_IsUndeclared() {
        var report = DriftReport.Compare(Registry(Of("READ_ME")), [Env("READ_ME"), Env("ADMIN_DISCORD_IDS")]);

        Assert.Equal(["ADMIN_DISCORD_IDS"], report.Undeclared.Select(e => e.Key));
        Assert.Equal(["READ_ME"], report.Matched.Select(e => e.Key));
        Assert.False(report.IsClean);
    }

    [Fact]
    public void ImageAndRuntimeKeys_AreNeverUndeclared() {
        var report = DriftReport.Compare(
            Registry(), [Env("APP_UID", EnvOrigin.Image), Env("ASPNET_VERSION", EnvOrigin.Image), Env("HOSTNAME", EnvOrigin.Runtime)]);

        Assert.True(report.IsClean);
        Assert.Empty(report.Entries);
    }

    [Fact]
    public void ImageOriginWins_WhenSameKeyAlsoInContainerEnv() {
        var report = DriftReport.Compare(
            Registry(), [Env("ADB_SERVER_SOCKET", EnvOrigin.Runtime), Env("ADB_SERVER_SOCKET", EnvOrigin.Image)]);

        Assert.Empty(report.Undeclared);
    }

    [Fact]
    public void ExternalDescriptor_IsReportedAsExternal_NotUndeclared() {
        var report = DriftReport.Compare(Registry(Of("CAPTURE_IFACE", kind: SettingKind.External)), [Env("CAPTURE_IFACE")]);

        Assert.Equal(["CAPTURE_IFACE"], report.External.Select(e => e.Key));
        Assert.True(report.IsClean);
    }

    [Fact]
    public void UnreferencedStackVariable_IsDrift() {
        var report = DriftReport.Compare(
            Registry(), [Env("DATA_DIR", EnvOrigin.StackVariable), Env("OLD_THING", EnvOrigin.StackVariable, referenced: false)]);

        Assert.Equal(["OLD_THING"], report.UnusedStackVariables.Select(e => e.Key));
        Assert.False(report.IsClean);
    }

    [Fact]
    public void StackVariable_DoesNotCountAsPresent() {
        var report = DriftReport.Compare(Registry(Of("NEEDED", required: true)), [Env("NEEDED", EnvOrigin.StackVariable)]);

        Assert.Equal(["NEEDED"], report.MissingRequired.Select(e => e.Key));
    }

    [Fact]
    public void RequiredKeyMissing_IsMissingRequired() {
        var report = DriftReport.Compare(Registry(Of("NEEDED", required: true)), []);

        Assert.Equal(["NEEDED"], report.MissingRequired.Select(e => e.Key));
        Assert.False(report.IsClean);
    }

    [Fact]
    public void RequiredKeyWithDefault_IsNotDrift() {
        var report = DriftReport.Compare(Registry(Of("NEEDED", required: true, @default: "8090")), []);

        Assert.Empty(report.MissingRequired);
        Assert.True(report.IsClean);
    }

    [Fact]
    public void OptionalKeyMissing_IsInformationalOnly() {
        var report = DriftReport.Compare(Registry(Of("OPTIONAL")), []);

        Assert.Equal(["OPTIONAL"], report.MissingOptional.Select(e => e.Key));
        Assert.True(report.IsClean);
    }

    [Fact]
    public void DatabaseValue_SatisfiesMissingKey_ExceptBootstrap() {
        var registry = Registry(Of("LIVE_ONE", required: true), Of("BOOT_ONE", required: true, tier: ApplyTier.Bootstrap));
        var report = DriftReport.Compare(registry, [], new HashSet<string>(["live_one", "boot_one"], StringComparer.Ordinal));

        Assert.Equal(["BOOT_ONE"], report.MissingRequired.Select(e => e.Key));
    }

    [Fact]
    public void ProblemCount_CountsOnlyActionableReasons() {
        var registry = Registry(Of("OPT"), Of("REQ", required: true));
        var report = DriftReport.Compare(registry, [Env("STRAY"), Env("UNUSED", EnvOrigin.StackVariable, referenced: false)]);

        Assert.Equal(3, report.ProblemCount);
    }
}
