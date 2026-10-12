using EggIdentity.Filters;

namespace EggIdentity.UI;

public sealed class FilterDraftRow {
    public FilterCondition Head { get; set; } = FilterDraft.Blank;
    public List<FilterCondition> Or { get; } = [];

    public IEnumerable<FilterCondition> Members => Or.Prepend(Head);
}

public sealed record FilterDraftState(bool IsValid, bool HasIncomplete, IReadOnlyList<FilterProblem> Problems);

public sealed class FilterDraft {
    public static FilterCondition Blank { get; } = new("", "", "");

    public List<FilterDraftRow> Rows { get; } = [];

    public void Hydrate(FilterSet set) {
        ArgumentNullException.ThrowIfNull(set);
        Rows.Clear();
        for (var i = 0; i < set.And.Count; i++) {
            var row = new FilterDraftRow { Head = set.And[i] };
            if (i < set.Or.Count) row.Or.AddRange(set.Or[i]);
            Rows.Add(row);
        }
        EnsureTrailingRow();
    }

    public FilterSet Complete(IReadOnlyList<FilterFieldDef> fields, Func<FilterFieldDef, IReadOnlyList<FilterOpDef>>? opsFor = null) {
        ArgumentNullException.ThrowIfNull(fields);
        var and = new List<FilterCondition>();
        var or = new List<IReadOnlyList<FilterCondition>>();
        foreach (var row in Rows) {
            var valid = row.Members.Where(c => !FilterValidator.IsBlank(c) && FilterValidator.Check(c, fields, opsFor) is null).ToList();
            if (valid.Count == 0) continue;
            and.Add(valid[0]);
            or.Add(valid.Skip(1).ToList());
        }
        return new FilterSet(and, or);
    }

    public FilterDraftState State(IReadOnlyList<FilterFieldDef> fields, Func<FilterFieldDef, IReadOnlyList<FilterOpDef>>? opsFor = null) {
        ArgumentNullException.ThrowIfNull(fields);
        var problems = new List<FilterProblem>();
        for (var r = 0; r < Rows.Count; r++) {
            var members = Rows[r].Members.ToList();
            for (var m = 0; m < members.Count; m++)
                if (FilterValidator.Check(members[m], fields, opsFor) is { } reason) problems.Add(new FilterProblem(r, m, members[m], reason));
        }
        return new FilterDraftState(problems.Count == 0, problems.Count > 0, problems);
    }

    public bool IsRowComplete(int row, IReadOnlyList<FilterFieldDef> fields, Func<FilterFieldDef, IReadOnlyList<FilterOpDef>>? opsFor = null) =>
        row >= 0 && row < Rows.Count && !FilterValidator.IsBlank(Rows[row].Head) && FilterValidator.Check(Rows[row].Head, fields, opsFor) is null;

    public void EnsureTrailingRow() {
        if (Rows.Count == 0 || !FilterValidator.IsBlank(Rows[^1].Head)) Rows.Add(new FilterDraftRow());
    }

    public void Remove(int row, int member) {
        if (row < 0 || row >= Rows.Count) return;
        var target = Rows[row];
        if (member == 0) {
            if (target.Or.Count > 0) {
                target.Head = target.Or[0];
                target.Or.RemoveAt(0);
            } else {
                Rows.RemoveAt(row);
            }
        } else if (member - 1 < target.Or.Count) {
            target.Or.RemoveAt(member - 1);
        }
        EnsureTrailingRow();
    }
}
