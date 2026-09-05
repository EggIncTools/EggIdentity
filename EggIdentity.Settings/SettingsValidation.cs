using System.Globalization;
using System.Net;
using System.Text.Json;

namespace EggIdentity.Settings;

public static class SettingsValidation {
    private const string IdPunctuation = ".-_:/@";

    public static string? Validate(SettingDescriptor descriptor, string? value) {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(value))
            return descriptor.Required ? $"{descriptor.Label} is required" : null;

        return ValidateKind(descriptor.Kind, value, descriptor.EnumValues);
    }

    public static string? ValidateKind(SettingKind kind, string? value, IReadOnlyList<string> enumValues) {
        ArgumentNullException.ThrowIfNull(enumValues);
        if (string.IsNullOrWhiteSpace(value)) return null;

        return kind switch {
            SettingKind.Bool => bool.TryParse(value, out _) ? null : "expected true or false",
            SettingKind.Number => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? null : "expected a whole number",
            SettingKind.Duration => SettingsFormat.ParseDuration(value) is not null
                ? null : "expected a duration such as 30s, 5m or 1h",
            SettingKind.Url => Uri.TryCreate(value, UriKind.Absolute, out _) ? null : "expected an absolute URL",
            SettingKind.Snowflake => ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? null : "expected a Discord snowflake id",
            SettingKind.Enum => enumValues.Contains(value, StringComparer.Ordinal)
                ? null : $"expected one of: {string.Join(", ", enumValues)}",
            SettingKind.CidrList => ValidateCidrList(value),
            SettingKind.Json => ValidateJson(value),
            SettingKind.ReadOnly => "this setting is read-only",
            SettingKind.External => "this setting is read outside the application and cannot be stored",
            _ => null,
        };
    }

    public static string? ValidateRow(CollectionDescriptor descriptor, IReadOnlyDictionary<string, string?> values) {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(values);

        foreach (var name in values.Keys) {
            if (descriptor.FindField(name) is null) return $"unknown field \"{name}\"";
        }

        var idField = descriptor.FindField(descriptor.IdField)
            ?? throw new InvalidOperationException($"collection \"{descriptor.Key}\" has no field \"{descriptor.IdField}\"");
        var id = values.GetValueOrDefault(descriptor.IdField);
        if (string.IsNullOrWhiteSpace(id)) return $"{idField.Label} is required";
        if (!IsPathSafeToken(id)) return $"{idField.Label} may only contain letters, digits and {IdPunctuation}";

        foreach (var field in descriptor.Fields) {
            var value = values.GetValueOrDefault(field.Name);
            if (string.IsNullOrWhiteSpace(value)) {
                if (field.Required) return $"{field.Label} is required";
                continue;
            }
            if (ValidateKind(field.Kind, value, field.EnumValues) is string error) return $"{field.Label}: {error}";
        }
        return null;
    }

    private static bool IsPathSafeToken(string id) =>
        id.All(c => char.IsAsciiLetterOrDigit(c) || IdPunctuation.Contains(c, StringComparison.Ordinal));

    private static string? ValidateCidrList(string value) {
        foreach (var entry in SettingsFormat.ParseList(value)) {
            var slash = entry.IndexOf('/', StringComparison.Ordinal);
            if (slash <= 0) return $"\"{entry}\" is not CIDR notation";
            if (!IPAddress.TryParse(entry[..slash], out var address)) return $"\"{entry}\" has an invalid address";
            if (!int.TryParse(entry[(slash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bits))
                return $"\"{entry}\" has an invalid prefix length";
            var max = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
            if (bits < 0 || bits > max) return $"\"{entry}\" prefix length must be between 0 and {max}";
        }
        return null;
    }

    private static string? ValidateJson(string value) {
        try {
            using var _ = JsonDocument.Parse(value);
            return null;
        } catch (JsonException e) {
            return e.Message;
        }
    }
}
