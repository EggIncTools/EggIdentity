namespace EggIdentity.Reports.Browser;

public sealed class HookedReportEngine(
    Func<string, IReportEngine?> primary,
    Func<string, IReportEngine?> fallback,
    Func<ReportQuery, ReportQuery> prepare,
    Func<ReportCube, ReportQuery, CancellationToken, Task<ReportCube>>? postProcess,
    Func<bool> primaryActive,
    Action<BrowserUnavailableException> onFallback) : IReportEngine {
    public async Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);
        var prepared = prepare(query);
        var cube = await RunEngineAsync(prepared, context, ct).ConfigureAwait(false);
        return postProcess is { } post ? await post(cube, prepared, ct).ConfigureAwait(false) : cube;
    }

    private async Task<ReportCube> RunEngineAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
        var server = fallback(query.Source);
        var browser = primary(query.Source) ?? throw new KeyNotFoundException($"no report engine for source \"{query.Source}\"");
        if (server is not null && !primaryActive()) return await server.RunAsync(query, context, ct).ConfigureAwait(false);
        try {
            return await browser.RunAsync(query, context, ct).ConfigureAwait(false);
        } catch (BrowserUnavailableException e) when (server is not null) {
            onFallback(e);
            return await server.RunAsync(query, context, ct).ConfigureAwait(false);
        }
    }
}
