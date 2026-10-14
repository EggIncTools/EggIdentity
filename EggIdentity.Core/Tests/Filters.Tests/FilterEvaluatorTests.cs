namespace EggIdentity.Filters.Tests;

public class FilterEvaluatorTests {
    private static readonly FilterFieldDef Ship = new("ship", "Ship", FilterValueKind.Select, FilterOps.Comparison) { Aliases = ["farm"] };
    private static readonly FilterFieldDef Ultra = new("ultra", "Ultra", FilterValueKind.Bool, FilterOps.Bool);
    private static readonly FilterFieldDef Version = new("version", "Version", FilterValueKind.Version, FilterOps.Comparison);
    private static readonly FilterFieldDef Drops = new("drops", "Drops", FilterValueKind.Set, FilterOps.ContainsOps);
    private static readonly FilterFieldDef Launch = new("launch", "Launch", FilterValueKind.Date, FilterOps.DayDate);
    private static readonly FilterFieldDef Rarity = new("artifact_rarity", "Rarity", FilterValueKind.Number, FilterOps.Comparison) { Scope = "artifact" };
    private static readonly FilterFieldDef[] Fields = [Ship, Ultra, Version, Drops, Launch, Rarity];

    private static readonly Dictionary<string, FilterSubjectValue> Subject = new() {
        ["ship"] = FilterSubjectValue.Of("3"),
        ["ultra"] = FilterSubjectValue.Of(true),
        ["version"] = FilterSubjectValue.Of("1.10.2"),
        ["drops"] = FilterSubjectValue.Of(["10_2_1", "11_3_0"]),
        ["launch"] = FilterSubjectValue.Of(new DateTimeOffset(2026, 10, 12, 18, 30, 0, TimeSpan.Zero)),
        ["artifact_rarity"] = FilterSubjectValue.Of(2d),
    };

    private static FilterResult Run(FilterSet set, FilterEvaluation? options = null) =>
        FilterEvaluator.Evaluate(set, Fields, key => Subject.GetValueOrDefault(key, FilterSubjectValue.Absent), options);

    private static FilterSet And(IReadOnlyList<FilterCondition> and) => new(and, []);

    private static FilterCondition C(string field, string op, string value) => new(field, op, value);

    [Fact]
    public void Empty_Matches() => Assert.True(Run(FilterSet.Empty).Matched);

    [Fact]
    public void EveryAndConditionMustPass() {
        Assert.True(Run(And([C("ship", "=", "3"), C("ultra", "true", "")])).Matched);
        var result = Run(And([C("ship", "=", "3"), C("ultra", "false", "")]));
        Assert.False(result.Matched);
        Assert.Equal("ultra", Assert.Single(result.Failed).Field);
    }

    [Fact]
    public void Row_PassesWhenHeadOrAnySiblingPasses() {
        Assert.True(Run(new FilterSet([new("ship", "=", "9")], [[new("ship", "=", "8"), new("ship", "=", "3")]])).Matched);
        Assert.True(Run(new FilterSet([new("ship", "=", "3")], [[new("ship", "=", "8")]])).Matched);
        var result = Run(new FilterSet([new("ship", "=", "9")], [[new("ship", "=", "8")]]));
        Assert.False(result.Matched);
        Assert.All(result.Failed, f => Assert.Equal(0, f.Row));
        Assert.Equal(2, result.Failed.Count);
    }

    [Fact]
    public void OrSiblings_PairWithTheirRowByPosition() {
        var set = new FilterSet([new("ultra", "true", ""), new("ship", "=", "9")], [[], [new("ship", "=", "3")]]);
        Assert.True(Run(set).Matched);
    }

    [Fact]
    public void ShortOrList_MeansNoSiblings() =>
        Assert.False(Run(new FilterSet([new("ultra", "true", ""), new("ship", "=", "9")], [[]])).Matched);

    [Fact]
    public void Failed_ListsEveryFailingCondition() =>
        Assert.Equal(2, Run(And([C("ship", "=", "9"), C("ultra", "false", "")])).Failed.Count);

    [Fact]
    public void Bool_AcceptsStoredAndInlineEncodings() {
        Assert.True(Run(And([C("ultra", "true", "")])).Matched);
        Assert.True(Run(And([C("ultra", "=", "true")])).Matched);
        Assert.True(Run(And([C("ultra", "eq", "true")])).Matched);
        Assert.False(Run(And([C("ultra", "=", "false")])).Matched);
    }

    [Fact]
    public void OpAndFieldAliases_Resolve() {
        Assert.True(Run(And([C("ship", "gte", "3")])).Matched);
        Assert.True(Run(And([C("farm", "eq", "3")])).Matched);
    }

    [Fact]
    public void CustomComparer_DecidesWhenItAnswers() {
        var gated = Drops with { Compare = (_, _, value) => value.EndsWith("_9", StringComparison.Ordinal) ? false : null };
        FilterFieldDef[] fields = [gated];
        var subject = Subject["drops"];
        Assert.False(FilterEvaluator.Evaluate(And([C("drops", "c", "10_%_%_9")]), fields, _ => subject).Matched);
        Assert.True(FilterEvaluator.Evaluate(And([C("drops", "c", "10_%_%")]), fields, _ => subject).Matched);
    }

    [Fact]
    public void Failure_CarriesTheCondition() {
        var failure = Assert.Single(Run(And([C("ship", "=", "9")])).Failed);
        Assert.Equal(C("ship", "=", "9"), failure.Condition);
    }

    [Fact]
    public void Select_OrderedOpsCompareCodesNumerically() {
        Assert.True(Run(And([C("ship", ">=", "3")])).Matched);
        Assert.True(Run(And([C("ship", ">", "2")])).Matched);
        Assert.False(Run(And([C("ship", "<", "3")])).Matched);
        Assert.False(Run(And([C("ship", ">", "10")]), new FilterEvaluation(Strict: false)).Matched);
    }

    [Fact]
    public void Version_ComparesSegmentsNumerically() {
        Assert.True(Run(And([C("version", ">", "1.9")])).Matched);
        Assert.False(Run(And([C("version", "<", "1.10")])).Matched);
    }

    [Fact]
    public void Set_GlobMatchesAnyValue() {
        Assert.True(Run(And([C("drops", "c", "10_%_%")])).Matched);
        Assert.True(Run(And([C("drops", "dnc", "12_%")])).Matched);
        Assert.False(Run(And([C("drops", "dnc", "11_%")])).Matched);
    }

    [Fact]
    public void DayDate_ComparesCalendarDay() =>
        Assert.True(Run(And([C("launch", "d=", "2026-10-12")])).Matched);

    [Fact]
    public void EmptyValue_IsIgnored() {
        var result = Run(And([C("ship", "=", "")]));
        Assert.True(result.Matched);
        Assert.Single(result.Ignored);
    }

    [Fact]
    public void RowOfOnlyIgnoredConditions_Passes() =>
        Assert.True(Run(new FilterSet([new("ship", "=", "")], [[new("ship", "=", "")]])).Matched);

    [Fact]
    public void Strict_FailsUnknownFieldBadOpAndUnparsableValue() {
        Assert.False(Run(And([C("gone", "=", "1")])).Matched);
        Assert.False(Run(And([C("ship", "c", "3")])).Matched);
        Assert.False(Run(And([C("version", ">", "abc")])).Matched);
    }

    [Fact]
    public void NonStrict_IgnoresUnknownFieldBadOpAndUnparsableValue() {
        var loose = new FilterEvaluation(Strict: false);
        var result = Run(And([C("gone", "=", "1"), C("ship", "c", "3"), C("version", ">", "abc")]), loose);
        Assert.True(result.Matched);
        Assert.Equal(3, result.Ignored.Count);
    }

    [Fact]
    public void WrongShapedSubject_FailsInBothModes() {
        var set = And([C("ultra", "=", "true")]);
        Assert.False(FilterEvaluator.Evaluate(set, Fields, WrongShape).Matched);
        Assert.False(FilterEvaluator.Evaluate(set, Fields, WrongShape, new FilterEvaluation(Strict: false)).Matched);
    }

    [Fact]
    public void Absent_PassesOnlyNegativeOps() {
        Assert.True(FilterEvaluator.Evaluate(And([C("ship", "!=", "3")]), Fields, Nothing).Matched);
        Assert.False(FilterEvaluator.Evaluate(And([C("ship", "=", "3")]), Fields, Nothing).Matched);
    }

    private static FilterSubjectValue WrongShape(string key) => FilterSubjectValue.Of("yes");

    private static FilterSubjectValue Nothing(string key) => FilterSubjectValue.Absent;

    private static readonly FilterFieldDef Launched = new("launched", "Launched", FilterValueKind.Date, FilterOps.Date);

    private static FilterResult Within(string value, DateTimeOffset at, FilterEvaluation options) =>
        FilterEvaluator.Evaluate(And([C("launched", FilterOps.Within, value)]), [Launched], _ => FilterSubjectValue.Of(at), options);

    [Fact]
    public void Within_MatchesValuesInsideTheWindow() {
        var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        var opts = new FilterEvaluation(Now: now);
        Assert.True(Within("30d", now.AddDays(-29), opts).Matched);
        Assert.False(Within("30d", now.AddDays(-31), opts).Matched);
        Assert.True(Within("2w", now.AddDays(-13), opts).Matched);
        Assert.False(Within("2w", now.AddDays(-15), opts).Matched);
    }

    [Fact]
    public void Within_MonthsAndYearsUseCalendarArithmetic() {
        var now = new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero);
        var opts = new FilterEvaluation(Now: now);
        Assert.True(Within("1m", new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero), opts).Matched);
        Assert.False(Within("1m", new DateTimeOffset(2026, 2, 27, 0, 0, 0, TimeSpan.Zero), opts).Matched);
        Assert.True(Within("1y", new DateTimeOffset(2025, 3, 31, 0, 0, 0, TimeSpan.Zero), opts).Matched);
        Assert.False(Within("1y", new DateTimeOffset(2025, 3, 30, 0, 0, 0, TimeSpan.Zero), opts).Matched);
    }

    [Fact]
    public void Within_WithoutNowOrWithBadValueIsUnparsable() {
        var at = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
        Assert.False(Within("30d", at, new FilterEvaluation()).Matched);
        var loose = Within("30d", at, new FilterEvaluation(Strict: false));
        Assert.True(loose.Matched);
        Assert.Single(loose.Ignored);
        Assert.False(Within("30x", at, new FilterEvaluation(Now: at)).Matched);
        Assert.Single(Within("abc", at, new FilterEvaluation(Strict: false, Now: at)).Ignored);
    }

    [Fact]
    public void Scope_RowWithNoInScopeMemberPasses() {
        var set = new FilterSet([new("artifact_rarity", "=", "9")], [[new("artifact_rarity", "=", "8")]]);
        Assert.True(Run(set, new FilterEvaluation(Scope: "mission")).Matched);
        Assert.False(Run(set, new FilterEvaluation(Scope: "artifact")).Matched);
    }

    [Fact]
    public void Scope_OutOfScopeMemberDoesNotRescueTheRow() {
        var set = new FilterSet([new("ship", "=", "9")], [[new("artifact_rarity", "=", "2")]]);
        Assert.False(Run(set, new FilterEvaluation(Scope: "mission")).Matched);
    }
}
