using EggIdentity.Contract;
using EggIdentity.Settings.Store;
using EggIdentity.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Deploy.AdminUi;

public sealed partial class FleetPanel : ComponentBase {
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);

    private SettingsCache? _cache;
    private AgentClient? _client;
    private ToastService? _toasts;

    [Parameter] public EventCallback OnOpenDeployApps { get; set; }

    private IReadOnlyList<DeployApp> _apps = [];
    private IReadOnlyList<DeployStatus>? _statuses;
    private IReadOnlyList<StackInfo> _stacks = [];
    private string? _agentError;
    private string? _stacksError;
    private bool _loaded;
    private bool _busy;
    private string? _armedStack;
    private DateTimeOffset _armedAt;

    private string AgentCss => _agentError is null ? "fleet-agent" : "fleet-agent fleet-agent-down";

    private string AgentSummary {
        get {
            if (_agentError is not null) return $"Agent unreachable: {_agentError}";
            if (_statuses is null) return "Agent: connecting";
            var updates = _statuses.Count(s => s.UpdateAvailable);
            return $"Agent tracks {_statuses.Count} app(s), {updates} update(s) available";
        }
    }

    private bool IsArmed(string stack) => _armedStack == stack && DateTimeOffset.UtcNow - _armedAt <= ConfirmWindow;
    private string RedeployLabel(string stack) => IsArmed(stack) ? "Confirm redeploy" : "Redeploy";

    protected override async Task OnInitializedAsync() {
        _cache = Services.GetService<SettingsCache>();
        _client = Services.GetService<AgentClient>();
        _toasts = Services.GetService<ToastService>();
        if (_cache is null || _client is null) return;

        var snapshot = await _cache.GetAsync();
        _apps = [.. snapshot.Collection<DeployApp>(DeployApps.Key).Where(a => a.Enabled)];
        _loaded = true;
        await LoadAgentAsync(_client);
    }

    private async Task LoadAgentAsync(AgentClient client) {
        try {
            _statuses = await client.GetAllStatusAsync(CancellationToken.None);
            _agentError = null;
        } catch (Exception e) when (IsAgentFailure(e)) {
            _agentError = e.Message;
            return;
        }

        try {
            _stacks = await client.GetStacksAsync(CancellationToken.None);
            _stacksError = null;
        } catch (Exception e) when (IsAgentFailure(e)) {
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
            await LoadAgentAsync(client);
        } finally {
            _busy = false;
        }

        if (failures.Count == 0) _toasts?.Push(StatusNoteKind.Ok, $"Checked {_apps.Count} app(s).");
        else _toasts?.Push(StatusNoteKind.Error, string.Join("; ", failures));
    }

    private static async Task<string?> TryCheckAsync(AgentClient client, string app) {
        try {
            await client.CheckAsync(app, CancellationToken.None);
            return null;
        } catch (Exception e) when (IsAgentFailure(e)) {
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
            await LoadAgentAsync(_client);
        } catch (Exception e) when (IsAgentFailure(e)) {
            _toasts?.Push(StatusNoteKind.Error, e.Message);
        } finally {
            _busy = false;
        }
    }

    private static bool IsAgentFailure(Exception e) =>
        e is HttpRequestException or TimeoutException or OperationCanceledException;
}
