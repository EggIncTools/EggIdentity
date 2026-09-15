using EggIdentity.Settings;
using EggIdentity.Settings.Store;

namespace EggIdentity.Settings.Api.Tests;

public class WireRoundTripTests {
    private static SettingDescriptor Descriptor() =>
        new("identity.api_port", "IDENTITY_API_PORT", "API port", "Identity",
            SettingKind.Number, ApplyTier.Bootstrap, Sensitivity.Plain) {
            Description = "port the API listens on",
            Required = true,
            Default = "8090",
            AllowBootstrapEdit = true,
            EnumValues = ["8090", "8091"],
        };

    [Fact]
    public void ASettingSurvivesTheRoundTrip() {
        var before = new SettingRow(Descriptor(), "8090", SettingSource.Database, true);

        var after = AdminWireMapping.FromWire(AdminWireMapping.ToWire(before));

        Assert.Equal(before.Descriptor.Key, after.Descriptor.Key);
        Assert.Equal(before.Descriptor.EnvKey, after.Descriptor.EnvKey);
        Assert.Equal(before.Descriptor.Kind, after.Descriptor.Kind);
        Assert.Equal(before.Descriptor.Tier, after.Descriptor.Tier);
        Assert.Equal(before.Descriptor.Required, after.Descriptor.Required);
        Assert.Equal(before.Descriptor.EnumValues, after.Descriptor.EnumValues);
        Assert.Equal(before.Display, after.Display);
        Assert.Equal(before.Source, after.Source);
        Assert.Equal(before.PendingRestart, after.PendingRestart);
    }

    [Fact]
    public void AllowBootstrapEditSurvives_SoALockedSettingStaysLocked() {
        var locked = new SettingRow(
            Descriptor() with { AllowBootstrapEdit = false }, "8090", SettingSource.Default, false);
        var unlocked = new SettingRow(Descriptor(), "8090", SettingSource.Default, false);

        Assert.False(AdminWireMapping.FromWire(AdminWireMapping.ToWire(locked)).Descriptor.AllowBootstrapEdit);
        Assert.True(AdminWireMapping.FromWire(AdminWireMapping.ToWire(unlocked)).Descriptor.AllowBootstrapEdit);
    }

    [Fact]
    public void EditableSurvives_BecauseItIsDerivedFromWhatTheWireCarries() {
        var locked = new SettingRow(
            Descriptor() with { AllowBootstrapEdit = false }, null, SettingSource.Default, false);

        Assert.False(locked.Descriptor.Editable);
        Assert.False(AdminWireMapping.FromWire(AdminWireMapping.ToWire(locked)).Descriptor.Editable);
    }

    [Fact]
    public void DefaultSurvives() =>
        Assert.Equal(
            "8090",
            AdminWireMapping.FromWire(AdminWireMapping.ToWire(
                new SettingRow(Descriptor(), null, SettingSource.Default, false))).Descriptor.Default);

    [Fact]
    public void ASecretStaysMarkedSecret() {
        var secret = new SettingRow(
            Descriptor() with { Sensitivity = Sensitivity.Secret }, SettingsAdminService.SecretMask,
            SettingSource.Database, false);

        Assert.True(AdminWireMapping.FromWire(AdminWireMapping.ToWire(secret)).Descriptor.IsSecret);
    }

    [Fact]
    public void ACollectionRowKeepsItsCollectionAndAudit() {
        var updated = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        var before = new CollectionRow(
            "deploy.apps", "eggledger",
            new Dictionary<string, string?> { ["name"] = "eggledger", ["tag"] = "v2.5.0" },
            updated, "david");

        var after = AdminWireMapping.FromWire(AdminWireMapping.ToWire(before));

        Assert.Equal("deploy.apps", after.Collection);
        Assert.Equal("eggledger", after.Id);
        Assert.Equal("v2.5.0", after.Get("tag"));
        Assert.Equal(updated, after.UpdatedAt);
        Assert.Equal("david", after.UpdatedBy);
    }

    [Fact]
    public void ACollectionDescriptorSurvives() {
        var before = new CollectionDescriptor(
            "deploy.apps", "Deployed apps", "Deploy",
            [
                new FieldDescriptor("name", "App name", SettingKind.Text) { Required = true },
                new FieldDescriptor("tag", "Tag", SettingKind.Text) { Description = "tag this environment runs" },
                new FieldDescriptor("deploy_secret", "Deploy secret", SettingKind.Secret, Sensitivity.Secret),
                new FieldDescriptor("environment", "Environment", SettingKind.Enum) {
                    EnumValues = ["prod", "subprod"],
                },
            ],
            "name", "name") {
            Description = "apps the agent watches",
        };

        var after = AdminWireMapping.FromWire(AdminWireMapping.ToWire(before));

        Assert.Equal(before.Key, after.Key);
        Assert.Equal(before.IdField, after.IdField);
        Assert.Equal(before.Description, after.Description);
        Assert.Equal(before.Fields.Select(f => f.Name), after.Fields.Select(f => f.Name));
        Assert.True(after.Fields.Single(f => f.Name == "deploy_secret").IsSecret);
        Assert.True(after.Fields.Single(f => f.Name == "name").Required);
        Assert.Equal(["prod", "subprod"], after.Fields.Single(f => f.Name == "environment").EnumValues);
    }

    [Fact]
    public void ADriftReportSurvives() {
        var before = new DriftReport([
            new DriftEntry("STRAY", EnvOrigin.EnvFile, DriftReason.Undeclared, "no descriptor declares this key"),
            new DriftEntry("MISSING", null, DriftReason.MissingRequired, "API secret"),
        ]);

        var after = AdminWireMapping.FromWire(AdminWireMapping.ToWire("eggledger", before));

        Assert.Equal(before.ProblemCount, after.ProblemCount);
        Assert.Equal(EnvOrigin.EnvFile, after.Entries[0].Origin);
        Assert.Equal(DriftReason.Undeclared, after.Entries[0].Reason);
        Assert.Null(after.Entries[1].Origin);
        Assert.Equal(DriftReason.MissingRequired, after.Entries[1].Reason);
    }

    [Fact]
    public void AnUnknownEnumStringFallsBackInsteadOfThrowing() {
        var wire = AdminWireMapping.ToWire(
            new SettingRow(Descriptor(), null, SettingSource.Default, false)) with {
            Kind = "SomethingAddedLater",
            Tier = "SomethingAddedLater",
        };

        var after = AdminWireMapping.FromWire(wire);

        Assert.Equal(SettingKind.Text, after.Descriptor.Kind);
        Assert.Equal(ApplyTier.Live, after.Descriptor.Tier);
    }
}
