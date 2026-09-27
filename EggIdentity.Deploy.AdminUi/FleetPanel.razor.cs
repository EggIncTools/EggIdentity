using EggIdentity.Contract;
using EggIdentity.Settings.Store;
using EggIdentity.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Deploy.AdminUi;

public sealed partial class FleetPanel : ComponentBase {
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);

    private SettingsCache? _cache;
    private FleetClient? _client;
    private ToastService? _toasts;

    [Parameter] public EventCallback OnOpenApps { get; set; }
    [Parameter] public bool ShowApps { get; set; } = true;

    private IReadOnlyList<SuiteApp> _apps = [];
    private IReadOnlyList<DeployStatus>? _statuses;
    private IReadOnlyList<StackInfo> _stacks = [];
    private string? _fleetError;
    private string? _stacksError;
    private bool _loaded;
    private bool _busy;
    private string? _armedStack;
    private DateTimeOffset _armedAt;

    private string FleetCss => _fleetError is null ? "fleet-summary" : "fleet-summary fleet-summary-down";

    private string FleetSummary {
        get {
            if (_fleetError is not null) return $"Fleet unreachable: {_fleetError}";
            if (_statuses is null) return "Fleet: connecting";
            var updates = _statuses.Count(s => s.UpdateAvailable);
            var problems = _statuses.Count(s => s.Problem is not null);
            return $"{_statuses.Count} app(s), {updates} update(s) available, {problems} with a problem";
        }
    }

    private bool IsArmed(string stack) => _armedStack == stack && DateTimeOffset.UtcNow - _armedAt <= ConfirmWindow;
    private string RedeployLabel(string stack) => IsArmed(stack) ? "Confirm redeploy" : "Redeploy";

    private IEnumerable<string> AppsOn(string stack) =>
        _apps.Where(a => string.Equals(a.Stack?.Trim(), stack, StringComparison.OrdinalIgnoreCase)).Select(a => a.Name);

    protected override async Task OnInitializedAsync() {
        _cache = Services.GetService<SettingsCache>();
        _client = Services.GetService<FleetClient>();
        _toasts = Services.GetService<ToastService>();
        if (_cache is null || _client is null) return;

        var snapshot = await _cache.GetAsync();
        _apps = [.. snapshot.Collection<SuiteApp>(SuiteApps.Key).Where(a => a.Enabled).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)];
        _loaded = true;
        await LoadFleetAsync(_client);
    }

    private async Task LoadFleetAsync(FleetClient client) {
        try {
            _statuses = await client.GetAllStatusAsync(CancellationToken.None);
            _fleetError = null;
        } catch (Exception e) when (IsFleetFailure(e)) {
            _fleetError = e.Message;
            return;
        }

        try {
            _stacks = await client.GetStacksAsync(CancellationToken.None);
            _stacksError = null;
        } catch (Exception e) when (IsFleetFailure(e)) {
            _stacksError = e.Message;
        }
    }

    private async Task CheckAllAsync() {
        if (_busy || _client is not { } client) return;
        _busy = true;
        var failures = new List<string>();
        try {
            foreach (var app in _apps) {
                if (await TryCheckAsync(client, app.Name) is { } failure) failures.Add(failure);
            }
            await LoadFleetAsync(client);
        } finally {
            _busy = false;
        }

        if (failures.Count == 0) _toasts?.Push(StatusNoteKind.Ok, $"Checked {_apps.Count} app(s).");
        else _toasts?.Push(StatusNoteKind.Error, string.Join("; ", failures));
    }

    private static async Task<string?> TryCheckAsync(FleetClient client, string app) {
        try {
            await client.CheckAsync(app, CancellationToken.None);
            return null;
        } catch (Exception e) when (IsFleetFailure(e)) {
            return $"{app}: {e.Message}";
        }
    }

    private async Task RedeployAsync(string stack) {
        if (_busy || _client is null) return;
        if (!IsArmed(stack)) {
            _armedStack = stack;
            _armedAt = DateTimeOffset.UtcNow;
            return;
        }

        _armedStack = null;
        _busy = true;
        try {
            var failure = await _client.RedeployStackAsync(stack, CancellationToken.None);
            if (failure is null) _toasts?.Push(StatusNoteKind.Ok, $"Redeploy of {stack} requested.");
            else _toasts?.Push(StatusNoteKind.Error, failure);
            await LoadFleetAsync(_client);
        } catch (Exception e) when (IsFleetFailure(e)) {
            _toasts?.Push(StatusNoteKind.Error, e.Message);
        } finally {
            _busy = false;
        }
    }

    private static bool IsFleetFailure(Exception e) =>
        e is HttpRequestException or TimeoutException or OperationCanceledException;
}
