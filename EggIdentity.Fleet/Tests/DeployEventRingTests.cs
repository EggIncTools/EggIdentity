using EggIdentity.Contract;

namespace EggIdentity.Fleet.Tests;

public class DeployEventRingTests {
    [Fact]
    public void Publish_AssignsMonotonicIdsFromFirstId() {
        var ring = new DeployEventRing(firstId: 1);

        var a = ring.Publish("app", DeployPhase.Checked, "one");
        var b = ring.Publish("app", DeployPhase.Checked, "two");

        Assert.Equal(1, a.Id);
        Assert.Equal(2, b.Id);
        Assert.Equal(2, ring.LastId);
    }

    [Fact]
    public void DefaultIds_AfterRestart_AreHigherThanBefore() {
        var before = new DeployEventRing(time: new FixedTimeProvider(new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero)));
        for (var i = 0; i < 1000; i++) before.Publish("app", DeployPhase.Checked, "x");
        var after = new DeployEventRing(time: new FixedTimeProvider(new DateTimeOffset(2026, 9, 26, 12, 0, 1, TimeSpan.Zero)));

        var next = after.Publish("app", DeployPhase.Checked, "after restart");

        Assert.True(next.Id > before.LastId);
    }

    [Fact]
    public void Since_ReturnsOnlyNewerEvents() {
        var ring = new DeployEventRing(firstId: 1);
        ring.Publish("app", DeployPhase.Checked, "one");
        ring.Publish("app", DeployPhase.Checked, "two");
        ring.Publish("app", DeployPhase.Checked, "three");

        Assert.Equal(["two", "three"], ring.Since(1).Select(e => e.Message));
    }

    [Fact]
    public void Publish_BeyondCapacity_DropsOldest() {
        var ring = new DeployEventRing(capacity: 3, firstId: 1);
        for (var i = 1; i <= 5; i++) ring.Publish("app", DeployPhase.Checked, "m" + i);

        Assert.Equal([3L, 4L, 5L], ring.Since(0).Select(e => e.Id));
    }

    [Fact]
    public void Latest_IsPerAppIgnoringCase() {
        var ring = new DeployEventRing();
        ring.Publish("a", DeployPhase.Pulling, "a1");
        ring.Publish("b", DeployPhase.Checked, "b1");
        ring.Publish("a", DeployPhase.Deployed, "a2");

        Assert.Equal("a2", ring.Latest("A")?.Message);
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
    public void Published_RaisesAndSurvivesThrowingHandler() {
        var ring = new DeployEventRing();
        var seen = new List<string>();
        ring.Published += _ => throw new InvalidOperationException("boom");
        ring.Published += e => seen.Add(e.Message);

        ring.Publish("app", DeployPhase.Failed, "bad");

        Assert.Equal(["bad"], seen);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
