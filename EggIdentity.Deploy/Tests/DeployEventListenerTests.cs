using System.IO.Pipelines;
using EggIdentity.Contract;

namespace EggIdentity.Deploy.Tests;

public class DeployEventListenerTests {
    [Fact]
    public async Task Listener_ReconnectsAfterFailure_AndReplaysFromLastEventId() {
        var firstStream = new Pipe();
        var idleStream = new Pipe();
        var thirdCall = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeAgentHandler((req, call) => call switch {
            1 => throw new HttpRequestException("connection refused"),
            2 => FakeAgentHandler.Stream(firstStream),
            _ => Record(req, thirdCall, idleStream),
        });
        await FakeAgentHandler.WriteAsync(firstStream, FakeAgentHandler.Frame(TestFixtures.Event(1, phase: DeployPhase.Pulling)));
        await FakeAgentHandler.WriteAsync(firstStream, FakeAgentHandler.Frame(TestFixtures.Event(2, phase: DeployPhase.Deployed)));
        await firstStream.Writer.CompleteAsync();

        var hub = new DeployEventHub();
        var reader = hub.Subscribe();
        var listener = new DeployEventListener(TestFixtures.Client(handler), hub, TestFixtures.Options());

        await listener.StartAsync(CancellationToken.None);
        var replayHeader = await thirdCall.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await listener.StopAsync(CancellationToken.None);

        Assert.Equal(1, (await reader.ReadAsync()).Id);
        Assert.Equal(2, (await reader.ReadAsync()).Id);
        Assert.Equal(2, hub.LastEventId);
        Assert.Equal("2", replayHeader);
        Assert.True(listener.Reconnects >= 2);
    }

    [Fact]
    public async Task Listener_StopsCleanly_WhileStreamIsOpen() {
        var open = new Pipe();
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new FakeAgentHandler((_, _) => {
            connected.TrySetResult();
            return FakeAgentHandler.Stream(open);
        });
        var listener = new DeployEventListener(TestFixtures.Client(handler), new DeployEventHub(), TestFixtures.Options());

        await listener.StartAsync(CancellationToken.None);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await listener.StopAsync(CancellationToken.None);

        Assert.True(listener.ExecuteTask!.IsCompletedSuccessfully);
    }

    private static HttpResponseMessage Record(HttpRequestMessage req, TaskCompletionSource<string?> signal, Pipe stream) {
        var header = req.Headers.TryGetValues("Last-Event-ID", out var values) ? values.FirstOrDefault() : null;
        signal.TrySetResult(header);
        return FakeAgentHandler.Stream(stream);
    }
}
