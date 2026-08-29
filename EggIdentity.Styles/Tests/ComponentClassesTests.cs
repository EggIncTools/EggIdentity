namespace EggIdentity.Styles.Tests;

public class ComponentClassesTests {
    [Fact]
    public void All_ContainsAtLeastOneKeyFromEachSet() {
        Assert.True(ComponentClasses.All.ContainsKey(".badge"));
        Assert.True(ComponentClasses.All.ContainsKey(".btn-primary"));
        Assert.True(ComponentClasses.All.ContainsKey(".panel"));
        Assert.True(ComponentClasses.All.ContainsKey(".segmented"));
        Assert.True(ComponentClasses.All.ContainsKey(".popover"));
        Assert.True(ComponentClasses.All.ContainsKey(".modal-card"));
        Assert.True(ComponentClasses.All.ContainsKey(".fab-bubble"));
        Assert.True(ComponentClasses.All.ContainsKey(".form-input"));
        Assert.True(ComponentClasses.All.ContainsKey(".data-table"));
        Assert.True(ComponentClasses.All.ContainsKey(".toast"));
        Assert.True(ComponentClasses.All.ContainsKey(".tooltip-floating"));
        Assert.True(ComponentClasses.All.ContainsKey(".prose-legal"));
    }

    [Fact]
    public void All_Count_EqualsSumOfAllSetsWithNoOverwrittenKeys() {
        var expected = Components.Badges.Applies.Count
            + Components.Buttons.Applies.Count
            + Components.Panels.Applies.Count
            + Components.SegmentedToggles.Applies.Count
            + Components.Popovers.Applies.Count
            + Components.Modals.Applies.Count
            + Components.FloatingBubbles.Applies.Count
            + Components.FormControls.Applies.Count
            + Components.DataTables.Applies.Count
            + Components.Toasts.Applies.Count
            + Components.Tooltips.Applies.Count
            + Components.Prose.Applies.Count;

        Assert.Equal(expected, ComponentClasses.All.Count);
    }
}
