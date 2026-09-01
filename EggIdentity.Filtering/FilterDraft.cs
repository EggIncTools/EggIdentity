namespace EggIdentity.Filtering;

public sealed class FilterConditionDraft<TField> where TField : notnull {
    public TField? Field { get; set; }
    public FilterOperator? Operator { get; set; }
    public FilterValue? Value { get; set; }

    public bool IsComplete => Field is not null && Operator is not null && Value is not null;
}

public sealed class FilterGroupDraft<TField> where TField : notnull {
    public List<FilterConditionDraft<TField>> Conditions { get; } = [];

    public void EnsureTrailingCondition() {
        if (Conditions.Count == 0 || Conditions[^1].IsComplete) Conditions.Add(new FilterConditionDraft<TField>());
    }

    public void RemoveConditionAt(int index) {
        Conditions.RemoveAt(index);
        if (Conditions.Count == 0) Conditions.Add(new FilterConditionDraft<TField>());
    }

    public IReadOnlyList<Condition<TField>> CompleteConditions() =>
        [.. Conditions.Where(c => c.IsComplete).Select(c => new Condition<TField>(c.Field!, c.Operator!.Value, c.Value!))];
}

public sealed class FilterDraft<TField> where TField : notnull {
    public List<FilterGroupDraft<TField>> Groups { get; } = [new()];

    public void EnsureTrailingGroup() {
        if (Groups.Count == 0 || Groups[^1].Conditions.Any(c => c.IsComplete)) Groups.Add(new FilterGroupDraft<TField>());
    }

    public void RemoveGroupAt(int index) {
        Groups.RemoveAt(index);
        if (Groups.Count == 0) Groups.Add(new FilterGroupDraft<TField>());
    }

    public Filter<TField> ToFilter() {
        var groups = Groups
            .Select(g => g.CompleteConditions())
            .Where(cs => cs.Count > 0)
            .Select(cs => new FilterGroup<TField>(cs))
            .ToList();
        return new Filter<TField>(groups);
    }
}
