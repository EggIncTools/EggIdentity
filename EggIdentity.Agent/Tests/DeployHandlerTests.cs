using EggIdentity.Agent;
using EggIdentity.Contract;

namespace EggIdentity.Agent.Tests;

public class DeployHandlerTests {
    [Fact]
    public void TryRun_NotInProgress_RunsDelegateAndReturnsRan() {
        var handler = new DeployHandler();
        var called = false;
        var (res, ran) = handler.TryRun(() => { called = true; return new DeployResponse { Ok = true }; });

        Assert.True(ran);
        Assert.True(called);
        Assert.True(res.Ok);
    }

    [Fact]
    public void TryRun_AlreadyInProgress_DoesNotRunDelegate() {
        var handler = new DeployHandler();
        Assert.True(handler.TryEnter());

        var called = false;
        var (res, ran) = handler.TryRun(() => { called = true; return new DeployResponse { Ok = true }; });

        Assert.False(ran);
        Assert.False(called);
        Assert.False(res.Ok);
    }
}
