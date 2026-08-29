namespace EggIdentity.UI.Tests;

file sealed class TestState : WorkbenchStateBase {
    public override IReadOnlyList<(string Key, string Label, int? Count)> Modes { get; } = [("list", "List", null), ("grid", "Grid", null)];
    public override string HashPrefix => "testWb";
}

file sealed class NoModesState : WorkbenchStateBase {
    public override IReadOnlyList<(string Key, string Label, int? Count)> Modes { get; } = [];
}

public class WorkbenchStateBaseTests {
    [Fact]
    public void DefaultMode_IsFirstModeKey() {
        var state = new TestState();
        Assert.Equal("list", state.DefaultMode);
    }

    [Fact]
    public void DefaultMode_EmptyWhenNoModes() {
        var state = new NoModesState();
        Assert.Equal("", state.DefaultMode);
    }

    [Fact]
    public void Mode_DefaultsToDefaultModeUntilSet() {
        var state = new TestState();
        Assert.Equal("list", state.Mode);
    }

    [Fact]
    public void Mode_SetToKnownKey_Sticks() {
        var state = new TestState { Mode = "grid" };
        Assert.Equal("grid", state.Mode);
    }

    [Fact]
    public void Mode_SetToUnknownKey_FallsBackToDefault() {
        var state = new TestState { Mode = "bogus" };
        Assert.Equal("list", state.Mode);
    }

    [Fact]
    public void OwnsHash_MatchesExactPrefix() {
        var state = new TestState();
        Assert.True(state.OwnsHash("testWb"));
        Assert.True(state.OwnsHash("#testWb"));
    }

    [Fact]
    public void OwnsHash_MatchesPrefixedSubHash() {
        var state = new TestState();
        Assert.True(state.OwnsHash("#testWb_details"));
    }

    [Fact]
    public void OwnsHash_RejectsUnrelatedHash() {
        var state = new TestState();
        Assert.False(state.OwnsHash("#otherThing"));
        Assert.False(state.OwnsHash("#testWbExtra"));
    }

    [Fact]
    public void OwnsHash_FalseWhenNoPrefixDeclared() {
        var state = new NoModesState();
        Assert.False(state.OwnsHash("#anything"));
    }
}
