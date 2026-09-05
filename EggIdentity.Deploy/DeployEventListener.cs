using EggIdentity.Resilience;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Deploy;

public sealed class DeployEventListener(
    AgentClient client, DeployEventHub hub, DeployOptions options, TimeProvider? time = null, ILogger<DeployEventListener>? logger = null) : BackgroundService {
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly RetryOptions _retry = options.ReconnectRetry;

    public int Reconnects { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested) {
            if (await RunOnceAsync(stoppingToken)) attempt = 0;
            attempt++;
            if (stoppingToken.IsCancellationRequested) return;
            Reconnects++;
            try {
                await Task.Delay(Backoff.Delay(attempt, _retry), _time, stoppingToken);
            } catch (OperationCanceledException) {
                return;
            }
        }
    }

    private async Task<bool> RunOnceAsync(CancellationToken ct) {
        var received = false;
        try {
            await foreach (var evt in client.StreamEventsAsync(hub.LastEventId, ct)) {
                received = true;
                hub.Publish(evt);
            }
            logger?.LogInformation("deploy event stream from {AgentUrl} ended, reconnecting", options.AgentUrl);
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
            return received;
        } catch (Exception e) {
            logger?.LogWarning(e, "deploy event stream from {AgentUrl} failed", options.AgentUrl);
        }
        return received;
    }
}
