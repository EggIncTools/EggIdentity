using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Hosting;

public abstract class PeriodicScopedService(IServiceScopeFactory scopes, TimeProvider time, ILogger logger) : BackgroundService {
    protected virtual bool Enabled => true;

    protected abstract TimeSpan Interval { get; }

    protected virtual bool RunAtStart => true;

    protected virtual string Name => GetType().Name;

    protected abstract Task RunOnceAsync(IServiceProvider services, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if (!Enabled) {
            logger.LogInformation("{Service} disabled", Name);
            return;
        }
        try {
            if (RunAtStart) await TickAsync(stoppingToken);
            using var timer = new PeriodicTimer(Interval, time);
            while (await timer.WaitForNextTickAsync(stoppingToken)) await TickAsync(stoppingToken);
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            logger.LogDebug("{Service} stopping", Name);
        }
    }

    private async Task TickAsync(CancellationToken ct) {
        try {
            await using var scope = scopes.CreateAsyncScope();
            await RunOnceAsync(scope.ServiceProvider, ct);
        } catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested) {
            logger.LogWarning(e, "{Service} tick failed; next run in {Interval}", Name, Interval);
        }
    }
}
