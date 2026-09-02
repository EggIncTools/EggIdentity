namespace EggIdentity.Settings;

public sealed record SettingDescriptor(
    string Key,
    string EnvKey,
    string Label,
    string Category,
    SettingKind Kind,
    ApplyTier Tier,
    Sensitivity Sensitivity) {
    public string? Description { get; init; }
    public bool Required { get; init; }
    public string? Default { get; init; }
    public IReadOnlyList<string> EnumValues { get; init; } = [];

    public bool Editable => Tier != ApplyTier.Bootstrap || AllowBootstrapEdit;

    public bool AllowBootstrapEdit { get; init; }

    public bool IsSecret => Sensitivity == Sensitivity.Secret;
}
