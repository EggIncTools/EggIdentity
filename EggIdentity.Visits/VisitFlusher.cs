using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Visits;

public sealed class VisitFlusher(
    VisitTracker tracker, IVisitsSink sink, VisitsOptions options, TimeProvider time, ILogger<VisitFlusher>? logger = null) : BackgroundService {
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
                logger?.LogWarning(e, "visits flush for {Site} on {Day} failed; that interval's counts are lost", options.Site, snapshot.Day);
            }
        }
    }

    private TimeSpan NextDelay() {
        var now = time.GetUtcNow();
        var midnight = new DateTimeOffset(DateOnly.FromDateTime(now.UtcDateTime).AddDays(1), TimeOnly.MinValue, TimeSpan.Zero);
        var untilMidnight = midnight - now;
        return untilMidnight < options.FlushInterval ? untilMidnight : options.FlushInterval;
    }
}
