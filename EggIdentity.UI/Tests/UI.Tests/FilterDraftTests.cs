using EggIdentity.Filters;

namespace EggIdentity.UI.Tests;

public class FilterDraftTests {
    private static readonly FilterFieldDef[] Fields = [new("ship", "Ship", FilterValueKind.Select, FilterOps.Comparison)];

    private static FilterSet Set(IReadOnlyList<FilterCondition> and, IReadOnlyList<IReadOnlyList<FilterCondition>> or) => new(and, or);

    [Fact]
    public void Hydrate_PairsOrSiblingsAndAddsTrailingRow() {
        var draft = new FilterDraft();
        draft.Hydrate(Set([new("ship", "=", "1"), new("ship", "=", "2")], [[], [new("ship", "=", "3")]]));

        Assert.Equal(3, draft.Rows.Count);
        Assert.Single(draft.Rows[1].Or);
        Assert.True(FilterValidator.IsBlank(draft.Rows[2].Head));
    }

    [Fact]
    public void Complete_RoundTripsAndSkipsBlankAndInvalid() {
        var draft = new FilterDraft();
        var set = Set([new("ship", "=", "1")], [[new("ship", "=", "3")]]);
        draft.Hydrate(set);
        draft.Rows[0].Or.Add(new FilterCondition("ship", "=", ""));

        var back = draft.Complete(Fields);

        Assert.Equal(set.And, back.And);
        Assert.Equal(set.Or[0], back.Or[0]);
    }

    [Fact]
    public void State_BlankTrailingRowIsNotIncomplete() {
        var draft = new FilterDraft();
        draft.Hydrate(Set([new("ship", "=", "1")], []));

        var state = draft.State(Fields);

        Assert.True(state.IsValid);
        Assert.False(state.HasIncomplete);
    }

    [Fact]
    public void State_HalfFilledRowIsIncomplete() {
        var draft = new FilterDraft();
        draft.Hydrate(Set([new("ship", "=", "")], []));

        Assert.True(draft.State(Fields).HasIncomplete);
    }

    [Fact]
    public void Remove_HeadPromotesFirstSiblingAndLastRowLeavesBlank() {
        var draft = new FilterDraft();
        draft.Hydrate(Set([new("ship", "=", "1")], [[new("ship", "=", "2")]]));

        draft.Remove(0, 0);
        Assert.Equal("2", draft.Rows[0].Head.Value);

        draft.Remove(0, 0);
        Assert.Single(draft.Rows);
        Assert.True(FilterValidator.IsBlank(draft.Rows[0].Head));
    }
}
