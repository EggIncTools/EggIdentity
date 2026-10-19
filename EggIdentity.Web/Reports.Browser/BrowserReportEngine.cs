using Microsoft.JSInterop;

namespace EggIdentity.Reports.Browser;

public sealed class BrowserWorkerClient(IJSRuntime js, string prefix, Func<string, ValueTask<string>> scopeKey) : IAsyncDisposable {
    private const string ModulePath = "./_content/EggIdentity.Web/reports-worker.js";
    private const string UnavailableMarker = "unavailable";
    private IJSObjectReference? _module;

    public async Task<string> RunAsync(string source, string? partition, string queryJson, string zoneId, string tier, CancellationToken ct) {
        try {
            var module = await ModuleAsync(ct).ConfigureAwait(false);
            var scope = await scopeKey(source).ConfigureAwait(false);
            return await module.InvokeAsync<string>("run", ct, prefix, source, scope, partition, queryJson, zoneId, tier).ConfigureAwait(false);
        } catch (JSException e) when (e.Message.Contains(UnavailableMarker, StringComparison.OrdinalIgnoreCase)) {
            throw new BrowserUnavailableException(e.Message, e);
        } catch (JSDisconnectedException e) {
            throw new BrowserUnavailableException(e.Message, e);
        } catch (InvalidOperationException e) when (e.Message.Contains("statically rendered", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("prerender", StringComparison.OrdinalIgnoreCase)) {
            throw new BrowserUnavailableException(e.Message, e, prerender: true);
        }
    }

    public async ValueTask InvalidateAsync(string? source, string? partition, CancellationToken ct) {
        if (_module is null) return;
        try {
            var scope = await scopeKey(source ?? "").ConfigureAwait(false);
            await _module.InvokeVoidAsync("invalidate", ct, scope, source, partition).ConfigureAwait(false);
        } catch (JSDisconnectedException) {
        } catch (BrowserUnavailableException) {
        }
    }

    public async ValueTask ClearAsync(CancellationToken ct) {
        try {
            var module = await ModuleAsync(ct).ConfigureAwait(false);
            var scope = await scopeKey("").ConfigureAwait(false);
            await module.InvokeVoidAsync("clear", ct, scope).ConfigureAwait(false);
        } catch (JSDisconnectedException) {
        } catch (BrowserUnavailableException) {
        }
    }

    private async ValueTask<IJSObjectReference> ModuleAsync(CancellationToken ct) {
        if (_module is { } ready) return ready;
        try {
            var module = await js.InvokeAsync<IJSObjectReference>("import", ct, ModulePath).ConfigureAwait(false);
            _module = module;
            return module;
        } catch (JSException e) {
            throw new BrowserUnavailableException(e.Message, e);
        }
    }

    public async ValueTask DisposeAsync() {
        if (_module is null) return;
        try {
            await _module.DisposeAsync().ConfigureAwait(false);
        } catch (JSDisconnectedException) {
        }
        _module = null;
    }
}

public sealed class BrowserReportEngine(BrowserWorkerClient client, string source) : IReportEngine {
    public Task<ReportCube> RunAsync(ReportQuery query, ReportContext context, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);
        var remote = new RemoteReportEngine((queryJson, zoneId, tier, token) => client.RunAsync(source, context.Partition, queryJson, zoneId, tier, token));
        return remote.RunAsync(query, context, ct);
    }
}
