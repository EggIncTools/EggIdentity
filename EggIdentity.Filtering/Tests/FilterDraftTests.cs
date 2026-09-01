using EggIdentity.Filtering;

namespace EggIdentity.Filtering.Tests;

public class FilterDraftTests {
    private enum TestField { Ship, Level }

    [Fact]
    public void EnsureTrailingCondition_addsWhenLastIsComplete() {
        var group = new FilterGroupDraft<TestField>();
        group.EnsureTrailingCondition();
        Assert.Single(group.Conditions);

        group.Conditions[0].Field = TestField.Ship;
        group.Conditions[0].Operator = FilterOperator.Equals;
        group.Conditions[0].Value = new FilterValue.EnumValue(0);

        group.EnsureTrailingCondition();
        Assert.Equal(2, group.Conditions.Count);
    }

    [Fact]
    public void EnsureTrailingCondition_doesNotAddWhenLastIsIncomplete() {
        var group = new FilterGroupDraft<TestField>();
        group.EnsureTrailingCondition();
        group.EnsureTrailingCondition();
        Assert.Single(group.Conditions);
    }

    [Fact]
    public void RemoveConditionAt_leavesOneEmptyRowWhenLastRemoved() {
        var group = new FilterGroupDraft<TestField>();
        group.EnsureTrailingCondition();

        group.RemoveConditionAt(0);

        Assert.Single(group.Conditions);
        Assert.False(group.Conditions[0].IsComplete);
    }

    [Fact]
    public void RemoveConditionAt_removesWithoutBackfillWhenOthersRemain() {
        var group = new FilterGroupDraft<TestField>();
        group.Conditions.Add(new FilterConditionDraft<TestField> { Field = TestField.Ship, Operator = FilterOperator.Equals, Value = new FilterValue.EnumValue(0) });
        group.Conditions.Add(new FilterConditionDraft<TestField> { Field = TestField.Level, Operator = FilterOperator.Equals, Value = new FilterValue.Number(1) });

        group.RemoveConditionAt(0);

        Assert.Single(group.Conditions);
        Assert.Equal(TestField.Level, group.Conditions[0].Field);
    }

    [Fact]
    public void CompleteConditions_skipsIncompleteRows() {
        var group = new FilterGroupDraft<TestField>();
        group.Conditions.Add(new FilterConditionDraft<TestField> { Field = TestField.Ship, Operator = FilterOperator.Equals, Value = new FilterValue.EnumValue(0) });
        group.Conditions.Add(new FilterConditionDraft<TestField> { Field = TestField.Level });

        var complete = group.CompleteConditions();

        Assert.Single(complete);
        Assert.Equal(TestField.Ship, complete[0].Field);
    }

    [Fact]
    public void EnsureTrailingGroup_addsWhenLastGroupHasCompleteCondition() {
        var draft = new FilterDraft<TestField>();
        draft.Groups[0].Conditions.Add(new FilterConditionDraft<TestField> { Field = TestField.Ship, Operator = FilterOperator.Equals, Value = new FilterValue.EnumValue(0) });

        draft.EnsureTrailingGroup();

        Assert.Equal(2, draft.Groups.Count);
    }

    [Fact]
    public void EnsureTrailingGroup_doesNotAddWhenLastGroupHasNoCompleteConditions() {
        var draft = new FilterDraft<TestField>();

        draft.EnsureTrailingGroup();

        Assert.Single(draft.Groups);
    }

    [Fact]
    public void RemoveGroupAt_leavesOneEmptyGroupWhenLastRemoved() {
        var draft = new FilterDraft<TestField>();

        draft.RemoveGroupAt(0);

        Assert.Single(draft.Groups);
        Assert.Empty(draft.Groups[0].Conditions);
    }

    [Fact]
    public void ToFilter_dropsFullyEmptyGroups() {
        var draft = new FilterDraft<TestField>();
        draft.Groups[0].Conditions.Add(new FilterConditionDraft<TestField> { Field = TestField.Ship, Operator = FilterOperator.Equals, Value = new FilterValue.EnumValue(0) });
        draft.Groups.Add(new FilterGroupDraft<TestField>());

        var filter = draft.ToFilter();

        Assert.Single(filter.Groups);
        Assert.Single(filter.Groups[0].Conditions);
        Assert.Equal(TestField.Ship, filter.Groups[0].Conditions[0].Field);
    }

    [Fact]
    public void ToFilter_producesEmptyFilterWhenNoCompleteConditionsAnywhere() {
        var draft = new FilterDraft<TestField>();

        var filter = draft.ToFilter();

        Assert.True(filter.IsEmpty);
        Assert.Empty(filter.Groups);
    }
}
