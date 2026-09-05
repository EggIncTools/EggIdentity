using System.Threading.Channels;
using EggIdentity.Contract;

namespace EggIdentity.Agent;

public sealed class DeployEventSubscription : IDisposable {
    private readonly Action _onDispose;

    internal DeployEventSubscription(ChannelReader<DeployEvent> reader, Action onDispose) {
        Reader = reader;
        _onDispose = onDispose;
    }

    public ChannelReader<DeployEvent> Reader { get; }

    public void Dispose() => _onDispose();
}

public sealed class DeployEventRing(int capacity = 500, TimeProvider? time = null) {
    private readonly TimeProvider _clock = time ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly Queue<DeployEvent> _events = new();
    private readonly List<Channel<DeployEvent>> _subscribers = [];
    private long _nextId;

    public int Capacity { get; } = capacity;

    public DeployEvent Publish(
        string app, DeployPhase phase, string message,
        string? fromRevision = null, string? toRevision = null, string? version = null, string? digest = null) {
        DeployEvent evt;
        Channel<DeployEvent>[] targets;
        lock (_gate) {
            evt = new DeployEvent(++_nextId, app, phase, message, _clock.GetUtcNow(), fromRevision, toRevision, version, digest);
            _events.Enqueue(evt);
            while (_events.Count > Capacity) _events.Dequeue();
            targets = [.. _subscribers];
        }
        foreach (var target in targets) target.Writer.TryWrite(evt);
        Console.WriteLine($"deploy: {app}: {phase}: {message}");
        return evt;
    }

    public IReadOnlyList<DeployEvent> Since(long lastId) {
        lock (_gate) {
            return [.. _events.Where(e => e.Id > lastId)];
        }
    }

    public DeployEvent? Latest(string app) {
        lock (_gate) {
            return _events.LastOrDefault(e => e.App == app);
        }
    }

    public long LastId {
        get {
            lock (_gate) {
                return _nextId;
            }
        }
    }

    public DeployEventSubscription Subscribe() {
        var channel = Channel.CreateUnbounded<DeployEvent>(new UnboundedChannelOptions { SingleReader = true });
        lock (_gate) {
            _subscribers.Add(channel);
        }
        return new DeployEventSubscription(channel.Reader, () => {
            lock (_gate) {
                _subscribers.Remove(channel);
            }
            channel.Writer.TryComplete();
        });
    }
}
