namespace EggIdentity.Filtering;

public enum FilterOperator {
    Equals, NotEquals, Greater, Less, GreaterOrEqual, LessOrEqual,
    Contains, NotContains, IsTrue, IsFalse,
}

public enum FilterValueKind {
    EnumCode, Number, Day, Flag,
}

public abstract record FilterValue {
    public sealed record EnumValue(int Code) : FilterValue;
    public sealed record Number(double N) : FilterValue;
    public sealed record Day(DateOnly Date) : FilterValue;
    public sealed record Flag(bool On) : FilterValue;
    public sealed record None : FilterValue {
        public static readonly None Instance = new();
    }
}

public static class FilterOperators {
    public static readonly IReadOnlyDictionary<FilterValueKind, IReadOnlyList<FilterOperator>> Applicable =
        new Dictionary<FilterValueKind, IReadOnlyList<FilterOperator>> {
            [FilterValueKind.EnumCode] = [FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Greater, FilterOperator.Less, FilterOperator.GreaterOrEqual, FilterOperator.LessOrEqual],
            [FilterValueKind.Number] = [FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Greater, FilterOperator.Less, FilterOperator.GreaterOrEqual, FilterOperator.LessOrEqual],
            [FilterValueKind.Day] = [FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Greater, FilterOperator.Less, FilterOperator.GreaterOrEqual, FilterOperator.LessOrEqual],
            [FilterValueKind.Flag] = [FilterOperator.IsTrue, FilterOperator.IsFalse],
        };
}

public sealed record Condition<TField>(TField Field, FilterOperator Operator, FilterValue Value) where TField : notnull;

public sealed record FilterGroup<TField>(IReadOnlyList<Condition<TField>> Conditions) where TField : notnull;

#pragma warning disable CA1000
public sealed record Filter<TField>(IReadOnlyList<FilterGroup<TField>> Groups) where TField : notnull {
    public static Filter<TField> Empty { get; } = new([]);

    public bool IsEmpty => Groups.Count == 0 || Groups.All(g => g.Conditions.Count == 0);
}
