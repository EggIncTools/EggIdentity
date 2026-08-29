namespace EggIdentity.Fallback.Tests;

public class MaintenanceStateTests {
    [Fact]
    public void DefaultsToOff() {
        var state = new MaintenanceState();
        Assert.False(state.IsOn);
    }

    [Fact]
    public void SetTrue_TurnsOn() {
        var state = new MaintenanceState();
        state.Set(true);
        Assert.True(state.IsOn);
    }

    [Fact]
    public void SetFalse_TurnsOff() {
        var state = new MaintenanceState();
        state.Set(true);
        state.Set(false);
        Assert.False(state.IsOn);
    }
}
