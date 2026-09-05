using EggIdentity.Contract;

namespace EggIdentity.Deploy.Tests;

public class DeployEventHubTests {
    [Fact]
    public void Publish_RaisesReceivedAndTracksLastEventId() {
        var hub = new DeployEventHub();
        var seen = new List<DeployEvent>();
        hub.Received += seen.Add;

        hub.Publish(TestFixtures.Event(4));
        hub.Publish(TestFixtures.Event(9));
        hub.Publish(TestFixtures.Event(6));

        Assert.Equal([4L, 9L, 6L], seen.Select(e => e.Id));
        Assert.Equal(9, hub.LastEventId);
    }

    [Fact]
    public async Task Subscribe_ReceivesPublishedEvents() {
        var hub = new DeployEventHub();
        var reader = hub.Subscribe();

        hub.Publish(TestFixtures.Event(1));
        hub.Publish(TestFixtures.Event(2));

        Assert.Equal(1, (await reader.ReadAsync()).Id);
        Assert.Equal(2, (await reader.ReadAsync()).Id);
    }

    [Fact]
    public async Task Subscribe_BoundedChannel_DropsOldest() {
        var hub = new DeployEventHub(subscriberCapacity: 2);
        var reader = hub.Subscribe();

        for (var i = 1; i <= 5; i++) hub.Publish(TestFixtures.Event(i));

        Assert.Equal(4, (await reader.ReadAsync()).Id);
        Assert.Equal(5, (await reader.ReadAsync()).Id);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void Unsubscribe_CompletesReaderAndStopsDelivery() {
        var hub = new DeployEventHub();
        var reader = hub.Subscribe();

        hub.Unsubscribe(reader);
        hub.Publish(TestFixtures.Event(1));

        Assert.True(reader.Completion.IsCompleted);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void Recent_FiltersByAppAndKeepsHistoryBounded() {
        var hub = new DeployEventHub(historyCapacity: 3);
        hub.Publish(TestFixtures.Event(1, app: "a"));
        hub.Publish(TestFixtures.Event(2, app: "b"));
        hub.Publish(TestFixtures.Event(3, app: "a"));
        hub.Publish(TestFixtures.Event(4, app: "A"));

        Assert.Equal([2L, 3L, 4L], hub.Recent().Select(e => e.Id));
        Assert.Equal([3L, 4L], hub.Recent("a").Select(e => e.Id));
    }

    [Fact]
    public void Publish_HandlerException_DoesNotStopOtherHandlers() {
        var hub = new DeployEventHub();
        var reached = false;
        hub.Received += _ => throw new InvalidOperationException("boom");
        hub.Received += _ => reached = true;

        hub.Publish(TestFixtures.Event(1));

        Assert.True(reached);
    }
}
