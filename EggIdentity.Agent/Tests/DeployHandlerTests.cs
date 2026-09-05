namespace EggIdentity.Agent.Tests;

public class DeployHandlerTests {
    [Fact]
    public void TryEnter_NotInProgress_EntersAndMarksBusy() {
        var handler = new DeployHandler();

        Assert.False(handler.InProgress);
        Assert.True(handler.TryEnter());
        Assert.True(handler.InProgress);
    }

    [Fact]
    public void TryEnter_AlreadyInProgress_Rejects() {
        var handler = new DeployHandler();
        Assert.True(handler.TryEnter());

        Assert.False(handler.TryEnter());
    }

    [Fact]
    public void Exit_ReleasesGate() {
        var handler = new DeployHandler();
        Assert.True(handler.TryEnter());

        handler.Exit();

        Assert.False(handler.InProgress);
        Assert.True(handler.TryEnter());
    }
}
