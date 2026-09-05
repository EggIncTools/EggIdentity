using EggIdentity.Contract;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Deploy.AdminUi;

public sealed partial class DeployPanel : ComponentBase, IDisposable {
    private const int TimelineLength = 30;
    private const int LogLines = 200;
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RefreshPeriod = TimeSpan.FromSeconds(30);

    private AgentClient? _client;
    private IDeployEvents? _events;
    private DeployOptions? _options;
    private DeployStatus? _status;
    private string? _error;
    private bool _working;
    private bool _recreating;
    private bool _logsOpen;
    private bool _logsLoading;
    private string? _logs;
    private string? _pendingConfirm;
    private DateTimeOffset _pendingConfirmAt;
    private CancellationTokenSource? _refreshCts;
    private readonly List<DeployEvent> _timeline = [];

    [Parameter] public string? App { get; set; }
    [Parameter] public bool ShowLogs { get; set; } = true;
    [Parameter] public bool Compact { get; set; }

    private string AppName => App ?? _options?.AppName ?? "";
    private bool IsOwnApp => _options is not null && string.Equals(AppName, _options.AppName, StringComparison.OrdinalIgnoreCase);
    private string RootClass => Compact ? "dp panel dp-compact" : "dp panel";
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;
    private IReadOnlyList<DeployEvent> Timeline => _timeline;
    private bool CanUpdate => !_working && _status is { UpdateAvailable: true, Busy: false };
    private string UpdateLabel => _pendingConfirm == "update" ? "Confirm update" : "Update now";
    private string RestartLabel => _pendingConfirm == "restart" ? "Confirm restart" : "Restart";
    private string RunningText => DeployPanelFormat.VersionLine(_status?.RunningVersion, _status?.RunningRevision, _status?.RunningDigest);
    private string LatestText => DeployPanelFormat.VersionLine(_status?.LatestVersion, _status?.LatestRevision, _status?.LatestDigest);
    private string CheckedText => DeployPanelFormat.Relative(_status?.LastCheckedAt, Now);
    private string? CheckedTitle => _status?.LastCheckedAt is { } at ? DeployPanelFormat.Timestamp(at) : null;

    protected override async Task OnInitializedAsync() {
        _client = Services.GetService<AgentClient>();
        _events = Services.GetService<IDeployEvents>();
        _options = Services.GetService<DeployOptions>();
        if (_client is null || _events is null || _options is null) return;

        SeedTimeline();
        _events.Received += OnEvent;
        await LoadStatusAsync();
        _refreshCts = new CancellationTokenSource();
        _ = RefreshLoopAsync(_refreshCts.Token);
    }

    private void SeedTimeline() {
        foreach (var evt in _events!.Recent(AppName).Reverse()) Append(evt);
    }

    private void Append(DeployEvent evt) {
        if (_timeline.Exists(e => e.Id == evt.Id)) return;
        _timeline.Insert(0, evt);
        if (_timeline.Count > TimelineLength) _timeline.RemoveRange(TimelineLength, _timeline.Count - TimelineLength);
    }

    private void OnEvent(DeployEvent evt) {
        if (!string.Equals(evt.App, AppName, StringComparison.OrdinalIgnoreCase)) return;
        Append(evt);
        if (evt.Phase == DeployPhase.Recreating && IsOwnApp) _recreating = true;
        if (evt.Phase is DeployPhase.Deployed or DeployPhase.Failed or DeployPhase.UpToDate or DeployPhase.Checked) _ = InvokeAsync(LoadStatusAsync);
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task LoadStatusAsync() {
        if (_client is null) return;
        try {
            _status = await _client.GetStatusAsync(AppName, CancellationToken.None);
            _error = _status is null ? $"Agent does not know an app named '{AppName}'." : null;
        } catch (Exception e) when (e is HttpRequestException or TimeoutException or OperationCanceledException) {
            _error = e.Message;
        }
        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshLoopAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(RefreshPeriod);
        try {
            while (await timer.WaitForNextTickAsync(ct)) await LoadStatusAsync();
        } catch (OperationCanceledException) {
            return;
        }
    }

    private Task CheckNowAsync() => RunAsync(async () => {
        _status = await _client!.CheckAsync(AppName, CancellationToken.None);
    });

    private Task UpdateNowAsync() {
        if (!Confirmed("update")) return Task.CompletedTask;
        return RunAsync(async () => {
            _status = await _client!.DeployAsync(AppName, CancellationToken.None);
        });
    }

    private Task RestartNowAsync() {
        if (!Confirmed("restart")) return Task.CompletedTask;
        return RunAsync(async () => {
            var failure = await _client!.RestartAsync(AppName, CancellationToken.None);
            if (failure is not null) _error = failure;
        });
    }

    private bool Confirmed(string action) {
        if (!IsOwnApp) return true;
        var now = Now;
        if (_pendingConfirm == action && now - _pendingConfirmAt <= ConfirmWindow) {
            _pendingConfirm = null;
            return true;
        }
        _pendingConfirm = action;
        _pendingConfirmAt = now;
        _ = ExpireConfirmAsync(action);
        return false;
    }

    private async Task ExpireConfirmAsync(string action) {
        try {
            var token = _refreshCts?.Token ?? CancellationToken.None;
            await Task.Delay(ConfirmWindow, token);
            if (_pendingConfirm != action) return;
            _pendingConfirm = null;
            await InvokeAsync(StateHasChanged);
        } catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException) {
        }
    }

    private async Task RunAsync(Func<Task> work) {
        if (_working || _client is null) return;
        _working = true;
        _error = null;
        try {
            await work();
        } catch (Exception e) when (e is HttpRequestException or TimeoutException or OperationCanceledException) {
            _error = e.Message;
        } finally {
            _working = false;
        }
    }

    private async Task ToggleLogsAsync() {
        if (_logsOpen) {
            _logsOpen = false;
            return;
        }
        _logsOpen = true;
        _logsLoading = true;
        try {
            _logs = await _client!.GetLogsTailAsync(AppName, LogLines, CancellationToken.None);
        } catch (Exception e) when (e is HttpRequestException or TimeoutException or OperationCanceledException) {
            _logs = e.Message;
        } finally {
            _logsLoading = false;
        }
    }

    public void Dispose() {
        Unsubscribe();
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private void Unsubscribe() {
        if (_events is null) return;
        _events.Received -= OnEvent;
    }
}
