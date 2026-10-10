using System.Threading.Channels;
using EggIdentity.Contract;

namespace EggIdentity.Deploy;

public sealed class DeployEventHub(int subscriberCapacity = 256, int historyCapacity = 500) : IDeployEvents {
    private readonly Lock _gate = new();
    private readonly List<Channel<DeployEvent>> _subscribers = [];
    private readonly Queue<DeployEvent> _history = new();
    private long _lastEventId;

    public event Action<DeployEvent>? Received;

    public long LastEventId {
        get {
            lock (_gate) return _lastEventId;
        }
    }

    public ChannelReader<DeployEvent> Subscribe() {
        var channel = Channel.CreateBounded<DeployEvent>(new BoundedChannelOptions(subscriberCapacity) {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        lock (_gate) _subscribers.Add(channel);
        return channel.Reader;
    }

    public void Unsubscribe(ChannelReader<DeployEvent> reader) {
        Channel<DeployEvent>? removed;
        lock (_gate) {
            removed = _subscribers.Find(c => ReferenceEquals(c.Reader, reader));
            if (removed is not null) _subscribers.Remove(removed);
        }
        removed?.Writer.TryComplete();
    }

    public IReadOnlyList<DeployEvent> Recent(string? app = null) {
        lock (_gate) {
            return app is null
                ? [.. _history]
                : [.. _history.Where(e => string.Equals(e.App, app, StringComparison.OrdinalIgnoreCase))];
        }
    }

    public void Publish(DeployEvent evt) {
        ArgumentNullException.ThrowIfNull(evt);
        Channel<DeployEvent>[] targets;
        lock (_gate) {
            if (evt.Id > _lastEventId) _lastEventId = evt.Id;
            _history.Enqueue(evt);
            while (_history.Count > historyCapacity) _history.Dequeue();
            targets = [.. _subscribers];
        }
        foreach (var target in targets) target.Writer.TryWrite(evt);
        Raise(evt);
    }

    private void Raise(DeployEvent evt) {
        if (Received is not { } handlers) return;
        foreach (var handler in handlers.GetInvocationList()) {
            try {
                ((Action<DeployEvent>)handler)(evt);
            } catch (Exception e) {
                Console.Error.WriteLine($"eggidentity-deploy: event handler failed: {e.Message}");
            }
        }
    }
}
