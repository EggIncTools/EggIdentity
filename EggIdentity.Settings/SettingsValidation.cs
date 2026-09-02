using System.Globalization;
using System.Net;
using System.Text.Json;

namespace EggIdentity.Settings;

public static class SettingsValidation {
    public static string? Validate(SettingDescriptor descriptor, string? value) {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(value))
            return descriptor.Required ? $"{descriptor.Label} is required" : null;

        return descriptor.Kind switch {
            SettingKind.Bool => bool.TryParse(value, out _) ? null : "expected true or false",
            SettingKind.Number => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? null : "expected a whole number",
            SettingKind.Duration => SettingsFormat.ParseDuration(value) is not null
                ? null : "expected a duration such as 30s, 5m or 1h",
            SettingKind.Url => Uri.TryCreate(value, UriKind.Absolute, out _) ? null : "expected an absolute URL",
            SettingKind.Snowflake => ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? null : "expected a Discord snowflake id",
            SettingKind.Enum => descriptor.EnumValues.Contains(value, StringComparer.Ordinal)
                ? null : $"expected one of: {string.Join(", ", descriptor.EnumValues)}",
            SettingKind.CidrList => ValidateCidrList(value),
            SettingKind.Json => ValidateJson(value),
            SettingKind.ReadOnly => "this setting is read-only",
            _ => null,
        };
    }

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
