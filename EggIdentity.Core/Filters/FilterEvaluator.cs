using System.Globalization;
using System.Text.RegularExpressions;

namespace EggIdentity.Filters;

public sealed record FilterEvaluation(string? Scope = null, bool Strict = true, DateTimeOffset? Now = null);

public sealed record FilterFailure(FilterCondition Condition, string Reason, int Row) {
    public string Field => Condition.Field;
}

public sealed record FilterResult(bool Matched, IReadOnlyList<FilterFailure> Failed, IReadOnlyList<FilterFailure> Ignored);

public static class FilterEvaluator {
    private enum Outcome {
        Pass,
        Fail,
        Ignore,
        OutOfScope,
    }

    private readonly record struct Verdict(Outcome Outcome, string Reason = "");

    private static readonly Verdict Pass = new(Outcome.Pass);
    private static readonly Verdict NoMatch = new(Outcome.Fail, "did not match");

    public static FilterResult Evaluate(
        FilterSet set, IReadOnlyList<FilterFieldDef> fields, Func<string, FilterSubjectValue> subject, FilterEvaluation? options = null) {
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(subject);
        var opts = options ?? new FilterEvaluation();
        var failed = new List<FilterFailure>();
        var ignored = new List<FilterFailure>();

        for (var row = 0; row < set.And.Count; row++) {
            var verdicts = Members(set, row).Select(c => (Condition: c, Verdict: Judge(c, fields, subject, opts))).ToList();
            ignored.AddRange(Collect(verdicts, Outcome.Ignore, row));
            if (verdicts.Any(v => v.Verdict.Outcome == Outcome.Pass)) continue;
            failed.AddRange(Collect(verdicts, Outcome.Fail, row));
        }

        return new FilterResult(failed.Count == 0, failed, ignored);
    }

    private static IEnumerable<FilterFailure> Collect(List<(FilterCondition Condition, Verdict Verdict)> verdicts, Outcome outcome, int row) =>
        verdicts.Where(v => v.Verdict.Outcome == outcome).Select(v => new FilterFailure(v.Condition, v.Verdict.Reason, row));

    private static IEnumerable<FilterCondition> Members(FilterSet set, int row) {
        yield return set.And[row];
        if (row < set.Or.Count)
            foreach (var sibling in set.Or[row]) yield return sibling;
    }

    private static Verdict Judge(FilterCondition c, IReadOnlyList<FilterFieldDef> fields, Func<string, FilterSubjectValue> subject, FilterEvaluation opts) {
        if (FilterFields.Find(fields, c.Field) is not { } field) return Reject(opts, $"unknown field \"{c.Field}\"");
        if (opts.Scope is { } scope && field.Scope is { } own && !string.Equals(scope, own, StringComparison.Ordinal))
            return new Verdict(Outcome.OutOfScope);

        var op = FilterOps.Canonical(c.Op);
        if (field.Kind == FilterValueKind.Bool && op is FilterOps.True or FilterOps.False)
            return Compare(subject(field.Key), field, FilterOps.Eq, op, opts.Now);
        if (string.IsNullOrWhiteSpace(c.Value)) return new Verdict(Outcome.Ignore, "empty value");
        if (!Allows(field, op)) return Reject(opts, $"op \"{c.Op}\" not allowed on {field.Key}");
        return Compare(subject(field.Key), field, op, c.Value.Trim(), opts.Now) is var v && v.Outcome == Outcome.Ignore ? Reject(opts, v.Reason) : v;
    }

    private static Verdict Reject(FilterEvaluation opts, string reason) =>
        new(opts.Strict ? Outcome.Fail : Outcome.Ignore, reason);

    private static bool Allows(FilterFieldDef field, string op) =>
        field.Ops.Any(o => string.Equals(o.Value, op, StringComparison.Ordinal))
        || (field.Kind == FilterValueKind.Bool && op == FilterOps.Eq);

    private static Verdict Compare(FilterSubjectValue actual, FilterFieldDef field, string op, string expected, DateTimeOffset? now) {
        if (field.Compare?.Invoke(actual, op, expected) is { } custom) return Result(custom);
        if (actual is FilterSubjectValue.AbsentValue)
            return op is FilterOps.NotEq or FilterOps.NotContains ? Pass : NoMatch;
        return field.Kind switch {
            FilterValueKind.Number => Numeric(actual, op, expected),
            FilterValueKind.Bool => Boolean(actual, expected),
            FilterValueKind.Date => Dated(actual, op, expected, now),
            FilterValueKind.Version => Versioned(actual, op, expected),
            FilterValueKind.Set => Membership(actual, op, expected),
            _ => Textual(actual, op, expected),
        };
    }

    private static Verdict Shape(FilterSubjectValue actual) =>
        new(Outcome.Fail, $"subject value {actual.GetType().Name} has the wrong shape");

    private static Verdict Unparsable(string expected) => new(Outcome.Ignore, $"value \"{expected}\" does not parse");

    private static Verdict Result(bool matched) => matched ? Pass : NoMatch;

    private static Verdict Ordered(int cmp, string op) => op switch {
        FilterOps.Eq => Result(cmp == 0),
        FilterOps.NotEq => Result(cmp != 0),
        FilterOps.Gt => Result(cmp > 0),
        FilterOps.Lt => Result(cmp < 0),
        FilterOps.Gte => Result(cmp >= 0),
        FilterOps.Lte => Result(cmp <= 0),
        _ => new Verdict(Outcome.Ignore, $"op \"{op}\" not supported"),
    };

    private static Verdict Numeric(FilterSubjectValue actual, string op, string expected) {
        if (actual is not FilterSubjectValue.NumberValue n) return Shape(actual);
        return double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var e) ? Ordered(n.Value.CompareTo(e), op) : Unparsable(expected);
    }

    private static Verdict Boolean(FilterSubjectValue actual, string expected) {
        if (actual is not FilterSubjectValue.BoolValue b) return Shape(actual);
        return bool.TryParse(expected, out var e) ? Result(b.Value == e) : Unparsable(expected);
    }

    private static Verdict Dated(FilterSubjectValue actual, string op, string expected, DateTimeOffset? now) {
        if (actual is not FilterSubjectValue.DateValue d) return Shape(actual);
        if (op == FilterOps.Within)
            return now is { } n && RelativeDate.Start(expected, n) is { } start ? Result(d.Value >= start) : Unparsable(expected);
        if (!DateTimeOffset.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var e)) return Unparsable(expected);
        if (op == FilterOps.DayEq) return Result(DateOnly.FromDateTime(d.Value.UtcDateTime) == DateOnly.FromDateTime(e.UtcDateTime));
        return Ordered(d.Value.CompareTo(e), op);
    }

    private static Verdict Versioned(FilterSubjectValue actual, string op, string expected) {
        if (actual is not FilterSubjectValue.TextValue t) return Shape(actual);
        if (Segments(t.Value) is not { } a) return new Verdict(Outcome.Fail, $"subject version \"{t.Value}\" does not parse");
        return Segments(expected) is { } e ? Ordered(CompareSegments(a, e), op) : Unparsable(expected);
    }

    private static Verdict Textual(FilterSubjectValue actual, string op, string expected) {
        var value = actual switch {
            FilterSubjectValue.TextValue t => t.Value,
            FilterSubjectValue.NumberValue n => n.Value.ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
        if (value is null) return Shape(actual);
        if (Number(value) is { } a && Number(expected) is { } e) return Ordered(a.CompareTo(e), op);
        return Ordered(string.Compare(value, expected, StringComparison.OrdinalIgnoreCase), op);
    }

    private static double? Number(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static Verdict Membership(FilterSubjectValue actual, string op, string expected) {
        if (actual is not FilterSubjectValue.ManyValue m) return Shape(actual);
        var any = m.Values.Any(v => Glob(expected).IsMatch(v));
        return op switch {
            FilterOps.Contains => Result(any),
            FilterOps.NotContains => Result(!any),
            _ => new Verdict(Outcome.Ignore, $"op \"{op}\" not supported"),
        };
    }

    private static Regex Glob(string pattern) =>
        new("^" + Regex.Escape(pattern).Replace("%", ".*", StringComparison.Ordinal) + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static long[]? Segments(string version) {
        var parts = version.Trim().Split('.');
        var result = new long[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (!long.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out result[i])) return null;
        return result;
    }

    private static int CompareSegments(long[] a, long[] b) {
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++) {
            var cmp = (i < a.Length ? a[i] : 0).CompareTo(i < b.Length ? b[i] : 0);
            if (cmp != 0) return cmp;
        }
        return 0;
    }
}
