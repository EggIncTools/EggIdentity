namespace EggIdentity.Host;

public sealed class IdentityReconcileService(IdentityReconciler reconciler, UserQueries users, TimeSpan interval) {
    public async Task RunAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(interval);
        try {
            await SweepAsync(ct);
            while (await timer.WaitForNextTickAsync(ct))
                await SweepAsync(ct);
        } catch (OperationCanceledException) { /* shutdown */ }
    }

    public async Task<int> SweepAsync(CancellationToken ct) {
        IReadOnlyList<Models.Identity> rows;
        try {
            rows = await users.ListAuthentikIdentitiesAsync(ct);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            Console.Error.WriteLine($"identity reconcile sweep: could not list users: {exc.Message}");
            return 0;
        }

        var touched = 0;
        foreach (var row in rows) {
            ct.ThrowIfCancellationRequested();
            try {
                var result = await reconciler.ReconcileAsync(row.UserId, row.Username!, row.Subject, ct);
                if (result.Applied && (result.Removed.Count > 0 || result.MergedUserIds.Count > 0 || result.Outcomes.Any(o => o.Outcome.Linked)))
                    touched++;
            } catch (Exception exc) when (exc is not OperationCanceledException) {
                Console.Error.WriteLine($"identity reconcile sweep for {row.UserId}: {exc.Message}");
            }
        }
        return touched;
    }

    public static async Task TryReconcileAsync(IServiceProvider services, Guid userId, string authentikUsername, string subject, CancellationToken ct) {
        var reconciler = services.GetService<IdentityReconciler>();
        if (reconciler is null) return;
        try {
            await reconciler.ReconcileAsync(userId, authentikUsername, subject, ct);
        } catch (Exception exc) when (exc is not OperationCanceledException) {
            Console.Error.WriteLine($"identity reconcile for {userId}: {exc.Message}");
        }
    }
}
