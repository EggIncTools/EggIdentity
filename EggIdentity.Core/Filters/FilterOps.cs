namespace EggIdentity.Filters;

public static class FilterOps {
    public const string Eq = "=";
    public const string NotEq = "!=";
    public const string Gt = ">";
    public const string Lt = "<";
    public const string Gte = ">=";
    public const string Lte = "<=";
    public const string DayEq = "d=";
    public const string Contains = "c";
    public const string NotContains = "dnc";
    public const string True = "true";
    public const string False = "false";

    public static IReadOnlyList<FilterOpDef> Equality { get; } = [new(Eq, "is"), new(NotEq, "is not")];

    public static IReadOnlyList<FilterOpDef> Comparison { get; } = [
        new(Eq, "is"),
        new(NotEq, "is not"),
        new(Gt, "greater than"),
        new(Lt, "less than"),
        new(Gte, "at least"),
        new(Lte, "at most"),
    ];

    public static IReadOnlyList<FilterOpDef> Date { get; } = [
        new(Eq, "on"),
        new(Lt, "before"),
        new(Gt, "after"),
        new(Lte, "on or before"),
        new(Gte, "on or after"),
    ];

    public static IReadOnlyList<FilterOpDef> DayDate { get; } = [new(DayEq, "on"), new(Lt, "before"), new(Gt, "after")];

    public static IReadOnlyList<FilterOpDef> ContainsOps { get; } = [new(Contains, "contains"), new(NotContains, "does not contain")];

    public static IReadOnlyList<FilterOpDef> Bool { get; } = [new(True, "True"), new(False, "False")];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal) {
        ["eq"] = Eq,
        ["neq"] = NotEq,
        ["gt"] = Gt,
        ["lt"] = Lt,
        ["gte"] = Gte,
        ["lte"] = Lte,
    };

    public static string Canonical(string op) {
        ArgumentNullException.ThrowIfNull(op);
        var trimmed = op.Trim();
        return Aliases.TryGetValue(trimmed, out var canonical) ? canonical : trimmed;
    }
}
