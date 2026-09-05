namespace EggIdentity.UI.Tests;

public class ToastServiceTests {
    private static (ToastService Service, FakeTimeProvider Time) Create() {
        var time = new FakeTimeProvider();
        return (new ToastService(time), time);
    }

    [Fact]
    public void Push_AddsItem() {
        var (svc, time) = Create();

        svc.Push(StatusNoteKind.Info, "hello");

        var item = Assert.Single(svc.Items);
        Assert.Equal(StatusNoteKind.Info, item.Kind);
        Assert.Equal("hello", item.Text);
        Assert.Equal(time.GetUtcNow(), item.At);
        Assert.False(item.Sticky);
    }

    [Fact]
    public void Push_BlankText_IsIgnored() {
        var (svc, _) = Create();

        svc.Push(StatusNoteKind.Info, "   ");

        Assert.Empty(svc.Items);
    }

    [Fact]
    public void Push_Error_IsSticky() {
        var (svc, _) = Create();

        svc.Push(StatusNoteKind.Error, "boom");

        Assert.True(Assert.Single(svc.Items).Sticky);
    }

    [Fact]
    public void Push_WithAction_IsSticky() {
        var (svc, _) = Create();

        svc.Push(StatusNoteKind.Info, "undo?", "Undo", () => { });

        var item = Assert.Single(svc.Items);
        Assert.True(item.Sticky);
        Assert.Equal("Undo", item.ActionLabel);
    }

    [Fact]
    public void Push_DuplicateWithinCollapseWindow_CollapsesAndRefreshesAt() {
        var (svc, time) = Create();
        svc.Push(StatusNoteKind.Info, "same");
        var first = Assert.Single(svc.Items);

        time.Advance(TimeSpan.FromSeconds(1));
        svc.Push(StatusNoteKind.Info, "same");

        var item = Assert.Single(svc.Items);
        Assert.Equal(first.Id, item.Id);
        Assert.Equal(first.At + TimeSpan.FromSeconds(1), item.At);
    }

    [Fact]
    public void Push_DuplicateOutsideCollapseWindow_AddsSecondItem() {
        var (svc, time) = Create();
        svc.Push(StatusNoteKind.Info, "same");

        time.Advance(TimeSpan.FromSeconds(2));
        svc.Push(StatusNoteKind.Info, "same");

        Assert.Equal(2, svc.Items.Count);
    }

    [Fact]
    public void Push_SixthItem_EvictsOldestNonSticky() {
        var (svc, _) = Create();
        for (var i = 0; i < 5; i++) svc.Push(StatusNoteKind.Info, $"item {i}");

        svc.Push(StatusNoteKind.Info, "item 5");

        Assert.Equal(5, svc.Items.Count);
        Assert.DoesNotContain(svc.Items, t => t.Text == "item 0");
        Assert.Contains(svc.Items, t => t.Text == "item 5");
    }

    [Fact]
    public void Push_StickySurvivesEviction_WhileNonStickyRemain() {
        var (svc, _) = Create();
        svc.Push(StatusNoteKind.Error, "sticky");
        for (var i = 0; i < 4; i++) svc.Push(StatusNoteKind.Info, $"item {i}");

        for (var i = 4; i < 9; i++) svc.Push(StatusNoteKind.Info, $"item {i}");

        Assert.Equal(5, svc.Items.Count);
        Assert.Contains(svc.Items, t => t.Text == "sticky");
        Assert.All(svc.Items.Where(t => t.Text != "sticky"), t => Assert.False(t.Sticky));
    }

    [Fact]
    public void Push_WhenOnlyStickyRemain_EvictsOldestSticky() {
        var (svc, _) = Create();
        for (var i = 0; i < 5; i++) svc.Push(StatusNoteKind.Error, $"err {i}");

        svc.Push(StatusNoteKind.Error, "err 5");

        Assert.Equal(5, svc.Items.Count);
        Assert.DoesNotContain(svc.Items, t => t.Text == "err 0");
        Assert.Contains(svc.Items, t => t.Text == "err 5");
    }

    [Fact]
    public void Dismiss_RemovesItemAndFiresChanged() {
        var (svc, _) = Create();
        svc.Push(StatusNoteKind.Info, "bye");
        var id = Assert.Single(svc.Items).Id;
        var fired = 0;
        svc.Changed += () => fired++;

        svc.Dismiss(id);

        Assert.Empty(svc.Items);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Dismiss_UnknownId_DoesNotFireChanged() {
        var (svc, _) = Create();
        var fired = 0;
        svc.Changed += () => fired++;

        svc.Dismiss(Guid.NewGuid());

        Assert.Equal(0, fired);
    }

    [Fact]
    public void Act_DismissesThenInvokesAction() {
        var (svc, _) = Create();
        var order = new List<string>();
        svc.Push(StatusNoteKind.Info, "undo?", "Undo", () => order.Add($"action:{svc.Items.Count}"));
        var id = Assert.Single(svc.Items).Id;
        svc.Changed += () => order.Add("changed");

        svc.Act(id);

        Assert.Empty(svc.Items);
        Assert.Equal(["changed", "action:0"], order);
    }

    [Fact]
    public void Act_WithoutAction_LeavesItemInPlace() {
        var (svc, _) = Create();
        svc.Push(StatusNoteKind.Info, "plain");
        var id = Assert.Single(svc.Items).Id;

        svc.Act(id);

        Assert.Single(svc.Items);
    }

    [Fact]
    public void Sweep_AfterLifetime_RemovesNonStickyAndStopsWhenOnlyStickyRemain() {
        var (svc, time) = Create();
        svc.Push(StatusNoteKind.Info, "fades");
        svc.Push(StatusNoteKind.Error, "stays");
        var fired = 0;
        svc.Changed += () => fired++;
        Assert.Equal(1, time.ActiveTimers);

        time.Advance(TimeSpan.FromSeconds(5));
        Assert.Equal(2, svc.Items.Count);

        time.Advance(TimeSpan.FromSeconds(1.5));

        var item = Assert.Single(svc.Items);
        Assert.Equal("stays", item.Text);
        Assert.Equal(1, fired);
        Assert.Equal(0, time.ActiveTimers);
    }

    [Fact]
    public void Push_StickyOnly_DoesNotStartSweep() {
        var (svc, time) = Create();

        svc.Push(StatusNoteKind.Error, "sticky");

        Assert.Equal(0, time.ActiveTimers);
    }

    [Fact]
    public void Dispose_StopsSweepAndIgnoresLaterPush() {
        var (svc, time) = Create();
        svc.Push(StatusNoteKind.Info, "one");

        svc.Dispose();
        svc.Push(StatusNoteKind.Info, "two");

        Assert.Equal(0, time.ActiveTimers);
        Assert.Single(svc.Items);
    }
}
