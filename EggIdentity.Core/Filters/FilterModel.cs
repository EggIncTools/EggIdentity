namespace EggIdentity.Filters;

public sealed record FilterCondition(string Field, string Op, string Value);

#pragma warning disable CA1000
public sealed record FilterSet(IReadOnlyList<FilterCondition> And, IReadOnlyList<IReadOnlyList<FilterCondition>> Or) {
    public static FilterSet Empty { get; } = new([], []);

    public bool IsEmpty => And.Count == 0;
}
#pragma warning restore CA1000

public enum FilterValueKind {
    Select,
    SearchableSelect,
    Number,
    Bool,
    Date,
    Text,
    Version,
    Set,
}

public sealed record FilterOpDef(string Value, string Label);

public sealed record FilterOption(string Value, string Label) {
    public string? Group { get; init; }
    public string? Badge { get; init; }
    public string? Image { get; init; }
    public string? Class { get; init; }
}

public delegate bool? FilterComparer(FilterSubjectValue actual, string op, string value);

public sealed record FilterFieldDef(string Key, string Label, FilterValueKind Kind, IReadOnlyList<FilterOpDef> Ops) {
    public string? Group { get; init; }
    public string? Scope { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public IReadOnlyList<FilterOption> Options { get; init; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public FilterComparer? Compare { get; init; }

    public bool Answers(string key) =>
        string.Equals(Key, key, StringComparison.Ordinal) || Aliases.Contains(key, StringComparer.Ordinal);
}

public sealed record FilterCatalog(string Subject, IReadOnlyList<FilterFieldDef> Fields, FilterSet Defaults);

public static class FilterFields {
    public static FilterFieldDef? Find(IReadOnlyList<FilterFieldDef> fields, string key) {
        ArgumentNullException.ThrowIfNull(fields);
        return fields.FirstOrDefault(f => f.Answers(key));
    }
}
