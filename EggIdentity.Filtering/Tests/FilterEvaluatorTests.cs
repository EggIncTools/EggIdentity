using EggIdentity.Filtering;

namespace EggIdentity.Filtering.Tests;

public class FilterEvaluatorTests {
    private enum TestField { Ship, Level, Target, DubCap, LaunchDate, Unregistered }
    private sealed record TestItem(int? Ship, double? Level, int? Target, bool? DubCap, DateOnly? LaunchDate);

    private static FilterEvaluator<TestItem, TestField> BuildEvaluator() =>
        new FilterEvaluator<TestItem, TestField>()
            .RegisterEnum(TestField.Ship, i => i.Ship)
            .RegisterNumber(TestField.Level, i => i.Level)
            .RegisterEnum(TestField.Target, i => i.Target)
            .RegisterFlag(TestField.DubCap, i => i.DubCap)
            .RegisterDay(TestField.LaunchDate, i => i.LaunchDate);

    private static Filter<TestField> SingleConditionFilter(TestField field, FilterOperator op, FilterValue value) =>
        new([new FilterGroup<TestField>([new Condition<TestField>(field, op, value)])]);

    [Fact]
    public async Task EnumEqualsAndNotEquals() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(0, null, null, null, null);

        Assert.True(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(0))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(1))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.NotEquals, new FilterValue.EnumValue(0))));
        Assert.True(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.NotEquals, new FilterValue.EnumValue(1))));
    }

    [Fact]
    public async Task NumberOrdering() {
        var evaluator = BuildEvaluator();
        var itemAt2 = new TestItem(null, 2, null, null, null);
        var itemAt1 = new TestItem(null, 1, null, null, null);

        Assert.True(await evaluator.MatchesAsync(itemAt2, SingleConditionFilter(TestField.Level, FilterOperator.Greater, new FilterValue.Number(1))));
        Assert.False(await evaluator.MatchesAsync(itemAt1, SingleConditionFilter(TestField.Level, FilterOperator.Greater, new FilterValue.Number(1))));
        Assert.True(await evaluator.MatchesAsync(itemAt1, SingleConditionFilter(TestField.Level, FilterOperator.Less, new FilterValue.Number(2))));
        Assert.False(await evaluator.MatchesAsync(itemAt2, SingleConditionFilter(TestField.Level, FilterOperator.Less, new FilterValue.Number(2))));
    }

    [Fact]
    public async Task EnumNegativeSentinel() {
        var evaluator = BuildEvaluator();
        var untargeted = new TestItem(null, null, -1, null, null);
        var targeted = new TestItem(null, null, 40, null, null);

        Assert.True(await evaluator.MatchesAsync(untargeted, SingleConditionFilter(TestField.Target, FilterOperator.Equals, new FilterValue.EnumValue(-1))));
        Assert.True(await evaluator.MatchesAsync(targeted, SingleConditionFilter(TestField.Target, FilterOperator.NotEquals, new FilterValue.EnumValue(41))));
    }

    [Fact]
    public async Task FlagIsTrueIsFalse() {
        var evaluator = BuildEvaluator();
        var dubbed = new TestItem(null, null, null, true, null);
        var notDubbed = new TestItem(null, null, null, false, null);

        Assert.True(await evaluator.MatchesAsync(dubbed, SingleConditionFilter(TestField.DubCap, FilterOperator.IsTrue, new FilterValue.Flag(true))));
        Assert.True(await evaluator.MatchesAsync(notDubbed, SingleConditionFilter(TestField.DubCap, FilterOperator.IsFalse, new FilterValue.Flag(false))));
        Assert.False(await evaluator.MatchesAsync(notDubbed, SingleConditionFilter(TestField.DubCap, FilterOperator.IsTrue, new FilterValue.Flag(false))));
    }

    [Fact]
    public async Task FlagOperatorIgnoresPayload() {
        var evaluator = BuildEvaluator();
        var dubbed = new TestItem(null, null, null, true, null);

        Assert.True(await evaluator.MatchesAsync(dubbed, SingleConditionFilter(TestField.DubCap, FilterOperator.IsTrue, new FilterValue.Flag(false))));
    }

    [Fact]
    public async Task NullEnumSemantics() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(null, null, null, null, null);

        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(0))));
        Assert.True(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.NotEquals, new FilterValue.EnumValue(0))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.Greater, new FilterValue.EnumValue(0))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.Less, new FilterValue.EnumValue(0))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.GreaterOrEqual, new FilterValue.EnumValue(0))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Ship, FilterOperator.LessOrEqual, new FilterValue.EnumValue(0))));
    }

    [Fact]
    public async Task NullNumberSemantics() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(null, null, null, null, null);

        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Level, FilterOperator.Equals, new FilterValue.Number(0))));
        Assert.True(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Level, FilterOperator.NotEquals, new FilterValue.Number(0))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Level, FilterOperator.Greater, new FilterValue.Number(0))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Level, FilterOperator.Less, new FilterValue.Number(0))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Level, FilterOperator.GreaterOrEqual, new FilterValue.Number(0))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.Level, FilterOperator.LessOrEqual, new FilterValue.Number(0))));
    }

    [Fact]
    public async Task NullDaySemantics() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(null, null, null, null, null);
        var day = new FilterValue.Day(new DateOnly(2024, 6, 1));

        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.LaunchDate, FilterOperator.Equals, day)));
        Assert.True(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.LaunchDate, FilterOperator.NotEquals, day)));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.LaunchDate, FilterOperator.Greater, day)));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.LaunchDate, FilterOperator.Less, day)));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.LaunchDate, FilterOperator.GreaterOrEqual, day)));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.LaunchDate, FilterOperator.LessOrEqual, day)));
    }

    [Fact]
    public async Task DateEquals() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(null, null, null, null, new DateOnly(2024, 6, 1));

        Assert.True(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.LaunchDate, FilterOperator.Equals, new FilterValue.Day(new DateOnly(2024, 6, 1)))));
        Assert.False(await evaluator.MatchesAsync(item, SingleConditionFilter(TestField.LaunchDate, FilterOperator.Equals, new FilterValue.Day(new DateOnly(2024, 6, 2)))));
    }

    [Fact]
    public async Task DateGreaterAndLess() {
        var evaluator = BuildEvaluator();
        var reference = new FilterValue.Day(new DateOnly(2024, 6, 1));

        var after = new TestItem(null, null, null, null, new DateOnly(2024, 6, 2));
        var before = new TestItem(null, null, null, null, new DateOnly(2024, 5, 31));

        Assert.True(await evaluator.MatchesAsync(after, SingleConditionFilter(TestField.LaunchDate, FilterOperator.Greater, reference)));
        Assert.False(await evaluator.MatchesAsync(before, SingleConditionFilter(TestField.LaunchDate, FilterOperator.Greater, reference)));
        Assert.True(await evaluator.MatchesAsync(before, SingleConditionFilter(TestField.LaunchDate, FilterOperator.Less, reference)));
    }

    [Fact]
    public async Task DateLessOrEqualIsInclusiveNotNoOp() {
        var evaluator = BuildEvaluator();
        var reference = new FilterValue.Day(new DateOnly(2024, 1, 1));
        var filter = SingleConditionFilter(TestField.LaunchDate, FilterOperator.LessOrEqual, reference);

        var after = new TestItem(null, null, null, null, new DateOnly(2024, 12, 31));
        var same = new TestItem(null, null, null, null, new DateOnly(2024, 1, 1));
        var before = new TestItem(null, null, null, null, new DateOnly(2023, 12, 31));

        Assert.False(await evaluator.MatchesAsync(after, filter));
        Assert.True(await evaluator.MatchesAsync(same, filter));
        Assert.True(await evaluator.MatchesAsync(before, filter));
    }

    [Fact]
    public async Task UnregisteredFieldPassesThroughButDoesNotRescueGroup() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(0, null, null, null, null);

        var passOnly = new Filter<TestField>([
            new FilterGroup<TestField>([
                new Condition<TestField>(TestField.Unregistered, FilterOperator.Equals, new FilterValue.EnumValue(99)),
            ]),
        ]);
        Assert.True(await evaluator.MatchesAsync(item, passOnly));

        var mixedWithFailingRealCondition = new Filter<TestField>([
            new FilterGroup<TestField>([
                new Condition<TestField>(TestField.Unregistered, FilterOperator.Equals, new FilterValue.EnumValue(99)),
                new Condition<TestField>(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(1)),
            ]),
        ]);
        Assert.False(await evaluator.MatchesAsync(item, mixedWithFailingRealCondition));
    }

    [Fact]
    public async Task EmptyFilterMatchesAnyItem() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(null, null, null, null, null);

        Assert.True(await evaluator.MatchesAsync(item, Filter<TestField>.Empty));
        Assert.True(await evaluator.MatchesAsync(item, new Filter<TestField>([])));
    }

    [Fact]
    public async Task ZeroConditionGroupMatchesAnyItem() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(null, null, null, null, null);

        var filter = new Filter<TestField>([new FilterGroup<TestField>([])]);
        Assert.True(await evaluator.MatchesAsync(item, filter));
    }

    [Fact]
    public async Task GroupsAreOredAcrossFilter() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(0, null, null, null, null);

        var filter = new Filter<TestField>([
            new FilterGroup<TestField>([
                new Condition<TestField>(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(1)),
            ]),
            new FilterGroup<TestField>([
                new Condition<TestField>(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(0)),
            ]),
        ]);

        Assert.True(await evaluator.MatchesAsync(item, filter));
    }

    [Fact]
    public async Task ConditionsWithinGroupAreAnded() {
        var evaluator = BuildEvaluator();
        var item = new TestItem(0, 5, null, null, null);

        var filter = new Filter<TestField>([
            new FilterGroup<TestField>([
                new Condition<TestField>(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(0)),
                new Condition<TestField>(TestField.Level, FilterOperator.Equals, new FilterValue.Number(6)),
            ]),
        ]);

        Assert.False(await evaluator.MatchesAsync(item, filter));
    }

    [Fact]
    public async Task AsyncConditionCanFailGroupAlongsidePassingSyncCondition() {
        var evaluator = BuildEvaluator().RegisterAsync(TestField.Ship, (_, _) => Task.FromResult(false));
        var item = new TestItem(0, 5, null, null, null);

        var filter = new Filter<TestField>([
            new FilterGroup<TestField>([
                new Condition<TestField>(TestField.Level, FilterOperator.Equals, new FilterValue.Number(5)),
                new Condition<TestField>(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(0)),
            ]),
        ]);

        Assert.False(await evaluator.MatchesAsync(item, filter));
    }

    [Fact]
    public async Task AsyncAndSyncConditionsBothPassingMatchesGroup() {
        var evaluator = BuildEvaluator().RegisterAsync(TestField.Ship, (_, _) => Task.FromResult(true));
        var item = new TestItem(0, 5, null, null, null);

        var filter = new Filter<TestField>([
            new FilterGroup<TestField>([
                new Condition<TestField>(TestField.Level, FilterOperator.Equals, new FilterValue.Number(5)),
                new Condition<TestField>(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(0)),
            ]),
        ]);

        Assert.True(await evaluator.MatchesAsync(item, filter));
    }

    [Fact]
    public async Task SyncConditionsAreEvaluatedBeforeAsyncOnes() {
        var order = new List<string>();
        var evaluator = new FilterEvaluator<TestItem, TestField>()
            .RegisterEnum(TestField.Level, i => {
                order.Add("sync");
                return i.Level.HasValue ? (int)i.Level.Value : null;
            })
            .RegisterAsync(TestField.Ship, (_, _) => {
                order.Add("async");
                return Task.FromResult(true);
            });
        var item = new TestItem(0, 5, null, null, null);

        var filter = new Filter<TestField>([
            new FilterGroup<TestField>([
                new Condition<TestField>(TestField.Ship, FilterOperator.Equals, new FilterValue.EnumValue(0)),
                new Condition<TestField>(TestField.Level, FilterOperator.Equals, new FilterValue.EnumValue(5)),
            ]),
        ]);

        await evaluator.MatchesAsync(item, filter);

        Assert.Equal(["sync", "async"], order);
    }
}
