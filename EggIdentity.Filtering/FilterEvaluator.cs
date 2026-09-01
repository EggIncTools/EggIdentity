namespace EggIdentity.Filtering;

public abstract record FieldAccessor<TItem>;

public sealed record EnumFieldAccessor<TItem>(Func<TItem, int?> Get) : FieldAccessor<TItem>;
public sealed record NumberFieldAccessor<TItem>(Func<TItem, double?> Get) : FieldAccessor<TItem>;
public sealed record DayFieldAccessor<TItem>(Func<TItem, DateOnly?> Get) : FieldAccessor<TItem>;
public sealed record FlagFieldAccessor<TItem>(Func<TItem, bool?> Get) : FieldAccessor<TItem>;

public sealed class FilterEvaluator<TItem, TField> where TField : notnull {
    private readonly Dictionary<TField, FieldAccessor<TItem>> _sync = [];
    private readonly Dictionary<TField, Func<TItem, Condition<TField>, Task<bool>>> _async = [];

    public FilterEvaluator<TItem, TField> RegisterEnum(TField field, Func<TItem, int?> get) {
        _sync[field] = new EnumFieldAccessor<TItem>(get);
        return this;
    }

    public FilterEvaluator<TItem, TField> RegisterNumber(TField field, Func<TItem, double?> get) {
        _sync[field] = new NumberFieldAccessor<TItem>(get);
        return this;
    }

    public FilterEvaluator<TItem, TField> RegisterDay(TField field, Func<TItem, DateOnly?> get) {
        _sync[field] = new DayFieldAccessor<TItem>(get);
        return this;
    }

    public FilterEvaluator<TItem, TField> RegisterFlag(TField field, Func<TItem, bool?> get) {
        _sync[field] = new FlagFieldAccessor<TItem>(get);
        return this;
    }

    public FilterEvaluator<TItem, TField> RegisterAsync(TField field, Func<TItem, Condition<TField>, Task<bool>> evaluate) {
        _async[field] = evaluate;
        return this;
    }

    public async Task<bool> MatchesAsync(TItem item, Filter<TField> filter) {
        if (filter.IsEmpty) return true;
        foreach (var group in filter.Groups) {
            if (await GroupMatchesAsync(item, group)) return true;
        }
        return false;
    }

    private async Task<bool> GroupMatchesAsync(TItem item, FilterGroup<TField> group) {
        if (group.Conditions.Count == 0) return true;
        foreach (var c in group.Conditions) {
            if (!_async.ContainsKey(c.Field) && !MatchesSync(item, c)) return false;
        }
        foreach (var c in group.Conditions) {
            if (_async.TryGetValue(c.Field, out var evaluate) && !await evaluate(item, c)) return false;
        }
        return true;
    }

    private bool MatchesSync(TItem item, Condition<TField> c) {
        if (!_sync.TryGetValue(c.Field, out var accessor)) return true;
        return accessor switch {
            EnumFieldAccessor<TItem> e => EnumMatch(e.Get(item), c),
            NumberFieldAccessor<TItem> n => NumberMatch(n.Get(item), c),
            DayFieldAccessor<TItem> d => DayMatch(d.Get(item), c),
            FlagFieldAccessor<TItem> f => FlagMatch(f.Get(item), c.Operator),
            _ => true,
        };
    }

    private static bool EnumMatch(int? actual, Condition<TField> c) {
        if (c.Value is not FilterValue.EnumValue e) return false;
        return c.Operator switch {
            FilterOperator.Equals => actual == e.Code,
            FilterOperator.NotEquals => actual != e.Code,
            FilterOperator.Greater => actual is { } a && a > e.Code,
            FilterOperator.Less => actual is { } a && a < e.Code,
            FilterOperator.GreaterOrEqual => actual is { } a && a >= e.Code,
            FilterOperator.LessOrEqual => actual is { } a && a <= e.Code,
            _ => false,
        };
    }

    private static bool NumberMatch(double? actual, Condition<TField> c) {
        if (c.Value is not FilterValue.Number n) return false;
        return c.Operator switch {
            FilterOperator.Equals => actual == n.N,
            FilterOperator.NotEquals => actual != n.N,
            FilterOperator.Greater => actual is { } a && a > n.N,
            FilterOperator.Less => actual is { } a && a < n.N,
            FilterOperator.GreaterOrEqual => actual is { } a && a >= n.N,
            FilterOperator.LessOrEqual => actual is { } a && a <= n.N,
            _ => false,
        };
    }

    private static bool DayMatch(DateOnly? actual, Condition<TField> c) {
        if (c.Value is not FilterValue.Day d) return false;
        return c.Operator switch {
            FilterOperator.Equals => actual == d.Date,
            FilterOperator.NotEquals => actual != d.Date,
            FilterOperator.Greater => actual is { } a && a > d.Date,
            FilterOperator.Less => actual is { } a && a < d.Date,
            FilterOperator.GreaterOrEqual => actual is { } a && a >= d.Date,
            FilterOperator.LessOrEqual => actual is { } a && a <= d.Date,
            _ => false,
        };
    }

    private static bool FlagMatch(bool? actual, FilterOperator op) => op switch {
        FilterOperator.IsTrue => actual == true,
        FilterOperator.IsFalse => actual == false,
        _ => false,
    };
}
