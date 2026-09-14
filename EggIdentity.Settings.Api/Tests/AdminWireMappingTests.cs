using EggIdentity.Settings.Store;

namespace EggIdentity.Settings.Api.Tests;

public class AdminWireMappingTests {
    private static SettingDescriptor Descriptor(
        string key = "a.one", SettingKind kind = SettingKind.Text, ApplyTier tier = ApplyTier.Live,
        Sensitivity sensitivity = Sensitivity.Plain) =>
        new(key, key.ToUpperInvariant(), "One", "Core", kind, tier, sensitivity) {
            Description = "what it does",
            Required = true,
        };

    [Fact]
    public void SettingRow_CarriesProvenanceAndEditability() {
        var row = new SettingRow(Descriptor(), "value", SettingSource.Database, PendingRestart: true);

        var wire = AdminWireMapping.ToWire(row);

        Assert.Equal("a.one", wire.Key);
        Assert.Equal("A.ONE", wire.EnvKey);
        Assert.Equal("Database", wire.Source);
        Assert.Equal("Text", wire.Kind);
        Assert.Equal("Live", wire.Tier);
        Assert.True(wire.Required);
        Assert.True(wire.Editable);
        Assert.True(wire.PendingRestart);
        Assert.False(wire.Secret);
    }

    [Fact]
    public void BootstrapSetting_ReportsItselfAsNotEditable() {
        var row = new SettingRow(
            Descriptor(tier: ApplyTier.Bootstrap), null, SettingSource.Environment, PendingRestart: false);

        Assert.False(AdminWireMapping.ToWire(row).Editable);
    }

    [Fact]
    public void SecretSetting_IsFlagged_AndCarriesOnlyTheMaskedDisplay() {
        var row = new SettingRow(
            Descriptor(kind: SettingKind.Secret, sensitivity: Sensitivity.Secret),
            SettingsAdminService.SecretMask, SettingSource.Database, PendingRestart: false);

        var wire = AdminWireMapping.ToWire(row);

        Assert.True(wire.Secret);
        Assert.Equal(SettingsAdminService.SecretMask, wire.Display);
    }

    [Fact]
    public void CollectionDescriptor_RoundTripsFieldsAndIdField() {
        var descriptor = new CollectionDescriptor(
            "deploy.apps", "Deployed apps", "Deploy",
            [
                new FieldDescriptor("name", "App name", SettingKind.Text) { Required = true },
                new FieldDescriptor("secret", "Secret", SettingKind.Secret, Sensitivity.Secret),
            ],
            "name", "name");

        var wire = AdminWireMapping.ToWire(descriptor);

        Assert.Equal("deploy.apps", wire.Key);
        Assert.Equal("name", wire.IdField);
        Assert.Equal(["name", "secret"], wire.Fields.Select(f => f.Name));
        Assert.True(wire.Fields[0].Required);
        Assert.True(wire.Fields[1].Secret);
    }

    [Fact]
    public void DriftReport_CarriesProblemCountAndEveryEntry() {
        var report = new DriftReport([
            new DriftEntry("STRAY", EnvOrigin.EnvFile, DriftReason.Undeclared, "no descriptor declares this key"),
            new DriftEntry("KNOWN", EnvOrigin.ServiceEnvironment, DriftReason.Matched, "fine"),
        ]);

        var wire = AdminWireMapping.ToWire("eggledger", report);

        Assert.Equal("eggledger", wire.App);
        Assert.True(wire.Available);
        Assert.Equal(1, wire.ProblemCount);
        Assert.Equal(["STRAY", "KNOWN"], wire.Entries.Select(e => e.Key));
        Assert.Equal("EnvFile", wire.Entries[0].Origin);
        Assert.Equal("Undeclared", wire.Entries[0].Reason);
    }

    [Fact]
    public void SaveResult_CarriesFailureReason() {
        var wire = AdminWireMapping.ToWire(new SettingsSaveResult(false, "is read-only", false));

        Assert.False(wire.Ok);
        Assert.Equal("is read-only", wire.Error);
        Assert.False(wire.RestartRequired);
    }
}
