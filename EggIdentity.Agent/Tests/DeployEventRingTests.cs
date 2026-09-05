using EggIdentity.Contract;

namespace EggIdentity.Agent.Tests;

public class DeployEventRingTests {
    [Fact]
    public void Publish_AssignsMonotonicIds() {
        var ring = new DeployEventRing();

        var a = ring.Publish("app", DeployPhase.Checked, "one");
        var b = ring.Publish("app", DeployPhase.Checked, "two");

        Assert.Equal(1, a.Id);
        Assert.Equal(2, b.Id);
        Assert.Equal(2, ring.LastId);
    }

    [Fact]
    public void Since_ReturnsOnlyNewerEvents() {
        var ring = new DeployEventRing();
        ring.Publish("app", DeployPhase.Checked, "one");
        ring.Publish("app", DeployPhase.Checked, "two");
        ring.Publish("app", DeployPhase.Checked, "three");

        var newer = ring.Since(1);

        Assert.Equal(["two", "three"], newer.Select(e => e.Message));
    }

    [Fact]
    public void Publish_BeyondCapacity_DropsOldest() {
        var ring = new DeployEventRing(capacity: 3);
        for (var i = 1; i <= 5; i++) ring.Publish("app", DeployPhase.Checked, "m" + i);

        var all = ring.Since(0);

        Assert.Equal([3L, 4L, 5L], all.Select(e => e.Id));
    }

    [Fact]
    public void Latest_IsPerApp() {
        var ring = new DeployEventRing();
        ring.Publish("a", DeployPhase.Pulling, "a1");
        ring.Publish("b", DeployPhase.Checked, "b1");
        ring.Publish("a", DeployPhase.Deployed, "a2");

        Assert.Equal("a2", ring.Latest("a")?.Message);
        Assert.Equal("b1", ring.Latest("b")?.Message);
        Assert.Null(ring.Latest("c"));
    }

    [Fact]
    public async Task Subscribe_ReceivesLiveEvents_UntilDisposed() {
        var ring = new DeployEventRing();
        var subscription = ring.Subscribe();

        ring.Publish("app", DeployPhase.Pulling, "live");
        var received = await subscription.Reader.ReadAsync();
        Assert.Equal("live", received.Message);

        subscription.Dispose();
        ring.Publish("app", DeployPhase.Pulled, "after");

        Assert.False(await subscription.Reader.WaitToReadAsync());
    }

    [Fact]
    public void Publish_UsesInjectedClock() {
        var at = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        var ring = new DeployEventRing(time: new FixedTimeProvider(at));

        var evt = ring.Publish("app", DeployPhase.Checked, "now", fromRevision: "abc", toRevision: "def", version: "v1", digest: "sha256:1");

        Assert.Equal(at, evt.At);
        Assert.Equal("abc", evt.FromRevision);
        Assert.Equal("def", evt.ToRevision);
        Assert.Equal("v1", evt.Version);
        Assert.Equal("sha256:1", evt.Digest);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
