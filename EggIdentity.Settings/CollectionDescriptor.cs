namespace EggIdentity.Settings;

public sealed record CollectionDescriptor(
    string Key,
    string Label,
    string Category,
    IReadOnlyList<FieldDescriptor> Fields,
    string IdField,
    string? DisplayField = null) {
    public string? Description { get; init; }
    public ApplyTier Tier { get; init; } = ApplyTier.Live;

    public bool HasSecrets => Fields.Any(f => f.IsSecret);

    public FieldDescriptor? FindField(string name) =>
        Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal));
}

public sealed record FieldDescriptor(
    string Name,
    string Label,
    SettingKind Kind,
    Sensitivity Sensitivity = Sensitivity.Plain) {
    public bool Required { get; init; }
    public string? Default { get; init; }
    public IReadOnlyList<string> EnumValues { get; init; } = [];
    public string? Description { get; init; }

    public bool IsSecret => Sensitivity == Sensitivity.Secret;
}

public interface ICollectionProvider {
    IReadOnlyList<CollectionDescriptor> Describe();
}

public sealed class StaticCollectionProvider(IReadOnlyList<CollectionDescriptor> descriptors) : ICollectionProvider {
    public IReadOnlyList<CollectionDescriptor> Describe() => descriptors;
}
