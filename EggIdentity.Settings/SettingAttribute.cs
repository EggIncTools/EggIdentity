namespace EggIdentity.Settings;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute(string key, string envKey, string label, string category, SettingKind kind) : Attribute {
    public string Key { get; } = key;
    public string EnvKey { get; } = envKey;
    public string Label { get; } = label;
    public string Category { get; } = category;
    public SettingKind Kind { get; } = kind;

    public ApplyTier Tier { get; init; } = ApplyTier.RestartRequired;
    public Sensitivity Sensitivity { get; init; } = Sensitivity.Plain;
    public string? Description { get; init; }
    public bool Required { get; init; }
    public string? Default { get; init; }
    public bool AllowBootstrapEdit { get; init; }
}
