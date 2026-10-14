using System.Globalization;

namespace EggIdentity.Filters;

public sealed record FilterProblem(int Row, int Member, FilterCondition Condition, string Reason);

public static class FilterValidator {
    public static IReadOnlyList<FilterProblem> Validate(
        FilterSet set, IReadOnlyList<FilterFieldDef> fields, Func<FilterFieldDef, IReadOnlyList<FilterOpDef>>? opsFor = null) {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(fields);
        var problems = new List<FilterProblem>();
        for (var row = 0; row < set.And.Count; row++) {
            var members = row < set.Or.Count ? [set.And[row], .. set.Or[row]] : new List<FilterCondition> { set.And[row] };
            for (var member = 0; member < members.Count; member++)
                if (Check(members[member], fields, opsFor) is { } reason) problems.Add(new FilterProblem(row, member, members[member], reason));
        }
        return problems;
    }

    public static bool IsBlank(FilterCondition condition) {
        ArgumentNullException.ThrowIfNull(condition);
        return string.IsNullOrWhiteSpace(condition.Field);
    }

    public static string? Check(FilterCondition condition, IReadOnlyList<FilterFieldDef> fields, Func<FilterFieldDef, IReadOnlyList<FilterOpDef>>? opsFor = null) {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(fields);
        if (IsBlank(condition)) return null;
        if (FilterFields.Find(fields, condition.Field) is not { } field) return $"unknown field \"{condition.Field}\"";
        var op = FilterOps.Canonical(condition.Op);
        if (field.Kind == FilterValueKind.Bool && op is FilterOps.True or FilterOps.False) return null;
        if (op.Length == 0) return "no operator";
        var ops = opsFor?.Invoke(field) ?? field.Ops;
        if (!ops.Any(o => o.Value == op) && !(field.Kind == FilterValueKind.Bool && op == FilterOps.Eq)) return $"op \"{condition.Op}\" not allowed on {field.Key}";
        if (string.IsNullOrWhiteSpace(condition.Value)) return "empty value";
        return Parses(field.Kind, op, condition.Value.Trim()) ? null : $"value \"{condition.Value}\" does not parse";
    }

    private static bool Parses(FilterValueKind kind, string op, string value) => kind switch {
        FilterValueKind.Date when op == FilterOps.Within => RelativeDate.Start(value, DateTimeOffset.MaxValue) is not null,
        FilterValueKind.Number => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
        FilterValueKind.Bool => bool.TryParse(value, out _),
        FilterValueKind.Date => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
        FilterValueKind.Version => value.Split('.').All(s => long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out _)),
        _ => true,
    };
}
