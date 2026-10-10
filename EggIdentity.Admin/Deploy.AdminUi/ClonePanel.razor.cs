using EggIdentity.Contract;
using EggIdentity.Settings.Api;
using EggIdentity.Settings.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Deploy.AdminUi;

public sealed class CloneRow(string app, AdminTargetStatus status) {
    public string App { get; } = app;
    public AdminTargetStatus Status { get; } = status;
    public CloneStatusResponse? Clone { get; set; }
    public string? Error { get; set; }

    public bool Active => string.Equals(Clone?.State, "running", StringComparison.Ordinal);
}

public sealed partial class ClonePanel : ComponentBase, IDisposable {
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollPeriod = TimeSpan.FromSeconds(3);

    private SettingsCache? _cache;
    private AdminApiClient? _client;
    private List<CloneRow>? _rows;
    private string? _error;
    private string? _note;
    private bool _busy;
    private string? _pendingApp;
    private DateTimeOffset _pendingAt;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    [Parameter] public string? App { get; set; }

    private bool AnyActive => _rows?.Exists(r => r.Active) == true;

    protected override async Task OnParametersSetAsync() {
        _cache ??= Services.GetService<SettingsCache>();
        _client ??= Services.GetService<AdminApiClient>();
        if (_cache is null || _client is null) return;
        await LoadAsync(_cache);
        if (AnyActive) EnsurePolling();
    }

    public void Dispose() {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
    }

    private static string StampText(CloneRow row) =>
        row.Clone?.LastStampAt is { } at ? DeployPanelFormat.Relative(at, DateTimeOffset.UtcNow) : "never";

    private static string? StampTitle(CloneRow row) =>
        row.Clone?.LastStampAt is { } at
            ? $"{DeployPanelFormat.Timestamp(at)} from {row.Clone.LastStampSource}"
            : null;

    private bool IsConfirming(string app) =>
        string.Equals(_pendingApp, app, StringComparison.Ordinal)
        && DateTimeOffset.UtcNow - _pendingAt < ConfirmWindow;

    private bool Arm(string app) {
        if (IsConfirming(app)) {
            _pendingApp = null;
            return true;
        }
        _pendingApp = app;
        _pendingAt = DateTimeOffset.UtcNow;
        _note = null;
        _error = null;
        return false;
    }

    private async Task CloneAsync(CloneRow row) {
        if (_client is not { } client || !Arm(row.App) || row.Status.Target is not { } target) return;
        _busy = true;
        _error = null;
        _note = null;
        try {
            var result = await client.StartCloneAsync(target, CancellationToken.None);
            if (!result.Ok) {
                _error = result.Error ?? $"{row.App} refused the clone";
                return;
            }
            _note = $"{row.App} clone started.";
            await RefreshAsync(row);
            EnsurePolling();
        } catch (Exception e) {
            _error = e.Message;
        } finally {
            _busy = false;
        }
    }

    private async Task LoadAsync(SettingsCache cache) {
        try {
            var snapshot = await cache.GetAsync();
            _rows = [.. snapshot.Collection<SuiteApp>(SuiteApps.Key)
                .Where(a => a.Enabled && a.IsSubProd)
                .Where(a => App is null || string.Equals(a.Name, App, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .Select(a => new CloneRow(a.Name, SuiteAppTargets.Describe(a, Environment.GetEnvironmentVariable)))];
            foreach (var row in _rows) await RefreshAsync(row);
        } catch (Exception e) {
            _rows = [];
            _error = e.Message;
        }
    }

    private async Task RefreshAsync(CloneRow row) {
        if (_client is not { } client || row.Status.Target is not { } target) return;
        try {
            row.Clone = await client.GetCloneStatusAsync(target, CancellationToken.None);
            row.Error = null;
        } catch (Exception e) when (e is HttpRequestException or TimeoutException or OperationCanceledException) {
            row.Error = e.Message;
        }
    }

    private void EnsurePolling() {
        if (_pollTask is { IsCompleted: false }) return;
        _pollCts?.Dispose();
        _pollCts = new CancellationTokenSource();
        _pollTask = PollLoopAsync(_pollCts.Token);
    }

    private async Task PollLoopAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(PollPeriod);
        try {
            while (await timer.WaitForNextTickAsync(ct)) {
                var active = _rows?.Where(r => r.Active).ToList() ?? [];
                if (active.Count == 0) break;
                foreach (var row in active) await RefreshAsync(row);
                await InvokeAsync(StateHasChanged);
            }
        } catch (OperationCanceledException) {
            return;
        }
    }
}
