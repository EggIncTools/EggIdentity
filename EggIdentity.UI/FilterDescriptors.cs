namespace EggIdentity.UI;

public sealed record FilterOpDef(string Value, string Label);

public sealed record FilterOption(string Value, string Label);

public enum FilterValueKind { Select, Text, Modal, Version, Number, Date, Bool }

public sealed record FilterFieldDef(
    string Key,
    string Label,
    FilterValueKind Kind,
    IReadOnlyList<FilterOpDef> Ops,
    IReadOnlyList<FilterOption>? Options = null);
