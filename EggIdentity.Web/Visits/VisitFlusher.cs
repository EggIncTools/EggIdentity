using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Visits;

public sealed class VisitFlusher(
    VisitTracker tracker,
    VisitorHasher hasher,
    IVisitsSink sink,
    VisitsOptions options,
    TimeProvider time,
    ILogger<VisitFlusher>? logger = null) : BackgroundService {
    private DateOnly? _claimed;

    public override async Task StartAsync(CancellationToken cancellationToken) {
        await ClaimAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(NextDelay(), time, stoppingToken);
                await FlushAsync(stoppingToken);
            }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
            await FlushAsync(CancellationToken.None);
        }
    }

    public async Task FlushAsync(CancellationToken ct) {
        foreach (var snapshot in tracker.Flush()) {
            try {
                await sink.UpsertDayAsync(options.Site, snapshot, ct);
            } catch (Exception e) when (e is not OperationCanceledException) {
                tracker.Requeue(snapshot);
                logger?.LogWarning(e, "visits flush for {Site} on {Day} failed; retrying next interval", options.Site, snapshot.Day);
            }
        }
        await ClaimAsync(ct);
    }

    public async Task ClaimAsync(CancellationToken ct) {
        var (day, salt) = hasher.Current();
        if (_claimed == day) return;
        try {
            var state = await sink.ClaimDayAsync(options.Site, day, salt, ct);
            hasher.Seed(day, state.Salt);
            tracker.Seed(day, state.LastSeen);
            _claimed = day;
        } catch (Exception e) when (e is not OperationCanceledException) {
            logger?.LogWarning(e, "visits day state for {Site} on {Day} could not be loaded; retrying next interval", options.Site, day);
        }
    }

    private TimeSpan NextDelay() {
        var now = time.GetUtcNow();
        var midnight = new DateTimeOffset(DateOnly.FromDateTime(now.UtcDateTime).AddDays(1), TimeOnly.MinValue, TimeSpan.Zero);
        var untilMidnight = midnight - now;
        return untilMidnight < options.FlushInterval ? untilMidnight : options.FlushInterval;
    }
}
