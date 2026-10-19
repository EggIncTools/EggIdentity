using System.Collections.Concurrent;

namespace EggIdentity.UI;

public sealed class UploadStore(TimeProvider time) {
    public const string TicketField = "ticket";

    private readonly ConcurrentDictionary<string, TaskCompletionSource<UploadResult>> _tickets = new(StringComparer.Ordinal);

    public string Open() {
        var id = Guid.NewGuid().ToString("N");
        _tickets[id] = new TaskCompletionSource<UploadResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        return id;
    }

    public bool Complete(string? ticket, UploadResult result) {
        ArgumentNullException.ThrowIfNull(result);
        return ticket is not null && _tickets.TryGetValue(ticket, out var tcs) && tcs.TrySetResult(result);
    }

    public async Task<UploadResult?> WaitAsync(string ticket, TimeSpan timeout, CancellationToken ct = default) {
        if (!_tickets.TryGetValue(ticket, out var tcs)) return null;
        try {
            return await tcs.Task.WaitAsync(timeout, time, ct);
        } catch (TimeoutException) {
            return null;
        } finally {
            _tickets.TryRemove(ticket, out _);
        }
    }

    public void Cancel(string ticket) => _tickets.TryRemove(ticket, out _);
}
