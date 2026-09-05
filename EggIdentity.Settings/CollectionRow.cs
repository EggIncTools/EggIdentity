namespace EggIdentity.Settings;

public sealed record CollectionRow(
    string Collection,
    string Id,
    IReadOnlyDictionary<string, string?> Values,
    DateTimeOffset UpdatedAt,
    string? UpdatedBy) {
    public string? Get(string field) => Values.GetValueOrDefault(field);
}
