namespace EggIdentity.UI;

public sealed record ComboOption(string Value, string Label) {
    public string? Group { get; init; }
    public string? Badge { get; init; }
    public string? Image { get; init; }
    public string? Class { get; init; }

    internal bool IsRich => !string.IsNullOrEmpty(Image) || !string.IsNullOrEmpty(Badge);
}
