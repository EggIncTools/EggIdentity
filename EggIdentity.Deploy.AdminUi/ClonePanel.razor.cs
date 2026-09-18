using EggIdentity.Contract;
using EggIdentity.Settings;
using EggIdentity.Settings.Api;
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

    private IPromotionStore? _store;
    private AdminApiClient? _client;
    private List<CloneRow>? _rows;
    private string? _error;
    private string? _note;
    private bool _busy;
    private string? _pendingApp;
    private DateTimeOffset _pendingAt;
    private CancellationTokenSource? _pollCts;
    private Task? _pollTask;

    private bool AnyActive => _rows?.Any(r => r.Active) == true;

    protected override async Task OnInitializedAsync() {
        _store = Services.GetService<IPromotionStore>();
        _client = Services.GetService<AdminApiClient>();
        if (_store is null || _client is null) return;
        await LoadAsync();
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
        if (!Arm(row.App) || row.Status.Target is not { } target) return;
        _busy = true;
        _error = null;
        _note = null;
        try {
            var result = await _client!.StartCloneAsync(target, CancellationToken.None);
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

    private async Task LoadAsync() {
        try {
            var targets = await _store!.GetRowsAsync(AdminTargets.Key, CancellationToken.None);
            var apps = await _store.GetRowsAsync(DeployApps.Key, CancellationToken.None);
            var subProd = new HashSet<string>(
                apps.Select(r => CollectionBinder.Bind<DeployApp>(r.Values))
                    .Where(a => a.TracksLatest)
                    .Select(a => a.Name),
                StringComparer.OrdinalIgnoreCase);
            _rows = [.. targets
                .Select(r => CollectionBinder.Bind<AdminTargetRow>(r.Values))
                .Where(t => subProd.Contains(t.Name))
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => new CloneRow(t.Name, AdminTargets.Describe(t, Environment.GetEnvironmentVariable)))];
            foreach (var row in _rows) await RefreshAsync(row);
        } catch (Exception e) {
            _rows = [];
            _error = e.Message;
        }
    }

    private async Task RefreshAsync(CloneRow row) {
        if (row.Status.Target is not { } target) return;
        try {
            row.Clone = await _client!.GetCloneStatusAsync(target, CancellationToken.None);
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
