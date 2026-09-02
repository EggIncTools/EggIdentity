using System.Globalization;

namespace EggIdentity.Settings;

public sealed record SettingValue(SettingDescriptor Descriptor, string? Value, SettingSource Source) {
    public string Key => Descriptor.Key;

    public bool IsSet => !string.IsNullOrEmpty(Value);

    public string? Display => Descriptor.IsSecret && IsSet ? "********" : Value;

    public bool AsBool() =>
        bool.TryParse(Value, out var b) ? b : string.Equals(Value, "1", StringComparison.Ordinal);

    public int AsInt(int fallback = 0) =>
        int.TryParse(Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

    public TimeSpan? AsDuration() => SettingsFormat.ParseDuration(Value);

    public IReadOnlyList<string> AsList() => SettingsFormat.ParseList(Value);
}
