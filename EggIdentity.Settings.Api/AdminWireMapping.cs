using EggIdentity.Contract;
using EggIdentity.Settings;
using EggIdentity.Settings.Store;

namespace EggIdentity.Settings.Api;

public static class AdminWireMapping {
    public static AdminSettingWire ToWire(SettingRow row) {
        ArgumentNullException.ThrowIfNull(row);
        var d = row.Descriptor;
        return new AdminSettingWire {
            Key = d.Key,
            EnvKey = d.EnvKey,
            Label = d.Label,
            Category = d.Category,
            Kind = d.Kind.ToString(),
            Tier = d.Tier.ToString(),
            Source = row.Source.ToString(),
            Description = d.Description,
            Display = row.Display,
            Required = d.Required,
            Secret = d.IsSecret,
            Editable = d.Editable,
            PendingRestart = row.PendingRestart,
            AllowBootstrapEdit = d.AllowBootstrapEdit,
            Default = d.Default,
            EnumValues = d.EnumValues,
        };
    }

    public static AdminCollectionWire ToWire(CollectionDescriptor descriptor) {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new AdminCollectionWire {
            Key = descriptor.Key,
            Label = descriptor.Label,
            Category = descriptor.Category,
            Description = descriptor.Description,
            IdField = descriptor.IdField,
            Fields = [.. descriptor.Fields.Select(ToWire)],
        };
    }

    public static AdminFieldWire ToWire(FieldDescriptor field) {
        ArgumentNullException.ThrowIfNull(field);
        return new AdminFieldWire {
            Name = field.Name,
            Label = field.Label,
            Kind = field.Kind.ToString(),
            Description = field.Description,
            Required = field.Required,
            Secret = field.IsSecret,
            EnumValues = field.EnumValues,
        };
    }

    public static AdminCollectionRowWire ToWire(CollectionRow row) {
        ArgumentNullException.ThrowIfNull(row);
        return new AdminCollectionRowWire {
            Collection = row.Collection,
            Id = row.Id,
            Values = row.Values,
            UpdatedAt = row.UpdatedAt,
            UpdatedBy = row.UpdatedBy,
        };
    }

    public static AdminDriftEntryWire ToWire(DriftEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        return new AdminDriftEntryWire {
            Key = entry.Key,
            Reason = entry.Reason.ToString(),
            Origin = entry.Origin?.ToString(),
            Detail = entry.Detail,
        };
    }

    public static AdminDriftResponse ToWire(string app, DriftReport report) {
        ArgumentNullException.ThrowIfNull(report);
        return new AdminDriftResponse {
            App = app,
            Available = true,
            ProblemCount = report.ProblemCount,
            Entries = [.. report.Entries.Select(ToWire)],
        };
    }

    public static AdminSaveResponse ToWire(SettingsSaveResult result) {
        ArgumentNullException.ThrowIfNull(result);
        return new AdminSaveResponse {
            Ok = result.Ok,
            Error = result.Error,
            RestartRequired = result.RestartRequired,
        };
    }

    public static SettingRow FromWire(AdminSettingWire wire) {
        ArgumentNullException.ThrowIfNull(wire);
        var descriptor = new SettingDescriptor(
            wire.Key, wire.EnvKey, wire.Label, wire.Category,
            Parse(wire.Kind, SettingKind.Text), Parse(wire.Tier, ApplyTier.Live),
            wire.Secret ? Sensitivity.Secret : Sensitivity.Plain) {
            Description = wire.Description,
            Required = wire.Required,
            Default = wire.Default,
            AllowBootstrapEdit = wire.AllowBootstrapEdit,
            EnumValues = wire.EnumValues,
        };
        return new SettingRow(descriptor, wire.Display, Parse(wire.Source, SettingSource.Default), wire.PendingRestart);
    }

    public static CollectionDescriptor FromWire(AdminCollectionWire wire) {
        ArgumentNullException.ThrowIfNull(wire);
        return new CollectionDescriptor(
            wire.Key, wire.Label, wire.Category, [.. wire.Fields.Select(FromWire)], wire.IdField, wire.IdField) {
            Description = wire.Description,
        };
    }

    public static FieldDescriptor FromWire(AdminFieldWire wire) {
        ArgumentNullException.ThrowIfNull(wire);
        return new FieldDescriptor(
            wire.Name, wire.Label, Parse(wire.Kind, SettingKind.Text),
            wire.Secret ? Sensitivity.Secret : Sensitivity.Plain) {
            Description = wire.Description,
            Required = wire.Required,
            EnumValues = wire.EnumValues,
        };
    }

    public static CollectionRow FromWire(AdminCollectionRowWire wire) {
        ArgumentNullException.ThrowIfNull(wire);
        return new CollectionRow(wire.Collection, wire.Id, wire.Values, wire.UpdatedAt, wire.UpdatedBy);
    }

    public static DriftEntry FromWire(AdminDriftEntryWire wire) {
        ArgumentNullException.ThrowIfNull(wire);
        return new DriftEntry(
            wire.Key,
            Enum.TryParse<EnvOrigin>(wire.Origin, out var origin) ? origin : null,
            Parse(wire.Reason, DriftReason.Matched),
            wire.Detail);
    }

    public static DriftReport FromWire(AdminDriftResponse response) {
        ArgumentNullException.ThrowIfNull(response);
        return new DriftReport([.. response.Entries.Select(FromWire)]);
    }

    public static SettingsSaveResult FromWire(AdminSaveResponse response) {
        ArgumentNullException.ThrowIfNull(response);
        return new SettingsSaveResult(response.Ok, response.Error, response.RestartRequired);
    }

    private static T Parse<T>(string? text, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(text, out var value) ? value : fallback;
}
