using EggIdentity.Contract;
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
        return new AdminCollectionRowWire { Id = row.Id, Values = row.Values };
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
}
