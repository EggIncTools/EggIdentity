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
    public string? KeyField { get; init; }
    public IReadOnlyList<string> SummaryFields { get; init; } = [];

    public bool HasSecrets => Fields.Any(f => f.IsSecret);

    public FieldDescriptor? FindField(string name) =>
        Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal));

    public string ResolvedKeyField =>
        KeyField is { Length: > 0 } key && FindField(key) is not null
            ? key
            : Fields.FirstOrDefault(f => f.Required)?.Name ?? IdField;

    public IReadOnlyList<FieldDescriptor> SummaryColumns() {
        var key = ResolvedKeyField;
        var named = SummaryFields.Count > 0
            ? SummaryFields.Select(FindField).OfType<FieldDescriptor>()
            : Fields.Where(f => f.Required || string.Equals(f.Name, DisplayField, StringComparison.Ordinal));
        var rest = named.Where(f => !string.Equals(f.Name, key, StringComparison.Ordinal)).DistinctBy(f => f.Name);
        return FindField(key) is { } first ? [first, .. rest] : [.. rest];
    }
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
    public bool Legacy { get; init; }
    public string? Group { get; init; }
    public FieldCondition? VisibleWhen { get; init; }
    public bool Nullable { get; init; }

    public bool IsSecret => Sensitivity == Sensitivity.Secret;

    public bool IsVisible(IReadOnlyDictionary<string, string?> values) {
        ArgumentNullException.ThrowIfNull(values);
        return VisibleWhen is not { } when
            || string.Equals(values.GetValueOrDefault(when.Field), when.Value, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record FieldCondition(string Field, string Value);

public interface ICollectionProvider {
    IReadOnlyList<CollectionDescriptor> Describe();
}

public sealed class StaticCollectionProvider(IReadOnlyList<CollectionDescriptor> descriptors) : ICollectionProvider {
    public IReadOnlyList<CollectionDescriptor> Describe() => descriptors;
}
