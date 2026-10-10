namespace EggIdentity.UI.Tests;

public class PageVisibilityTests {
    [Fact]
    public void ChangedFiresOnlyOnFlip() {
        var visibility = new PageVisibility(new FakeJSRuntime());
        var seen = new List<bool>();
        visibility.Changed += seen.Add;

        visibility.OnVisibilityChanged(true);
        visibility.OnVisibilityChanged(false);
        visibility.OnVisibilityChanged(false);
        visibility.OnVisibilityChanged(true);

        Assert.Equal([false, true], seen);
        Assert.True(visibility.Visible);
    }
}
