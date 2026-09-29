namespace EggIdentity.Styles.Tests;

public class ComponentClassesTests {
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
            + Components.Prose.Applies.Count
            + Components.Workbench.Applies.Count
            + Components.Filters.Applies.Count
            + Components.Calendar.Applies.Count
            + Components.Icons.Applies.Count
            + Components.Brand.Applies.Count
            + Components.Consent.Applies.Count;

        Assert.Equal(expected, ComponentClasses.All.Count);
    }
}
