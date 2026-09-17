using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Settings.Store;
using EggIdentity.UI;
using Microsoft.AspNetCore.Components;

namespace EggIdentity.Host.Components.Admin;

public sealed partial class FleetPane : ComponentBase {
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);

    [Inject] private SettingsCache Cache { get; set; } = default!;
    [Inject] private AgentClient Client { get; set; } = default!;
    [Inject] private ToastService Toasts { get; set; } = default!;

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
        var snapshot = await Cache.GetAsync();
        _apps = [.. snapshot.Collection<DeployApp>(DeployApps.Key).Where(a => a.Enabled)];
        _loaded = true;
        await LoadAgentAsync();
    }

    private async Task LoadAgentAsync() {
        try {
            _statuses = await Client.GetAllStatusAsync(CancellationToken.None);
            _agentError = null;
        } catch (Exception e) when (IsAgentFailure(e)) {
            _agentError = e.Message;
            return;
        }

        try {
            _stacks = await Client.GetStacksAsync(CancellationToken.None);
            _stacksError = null;
        } catch (Exception e) when (IsAgentFailure(e)) {
            _stacksError = e.Message;
        }
    }

    private async Task CheckAllAsync() {
        if (_busy) return;
        _busy = true;
        var failures = new List<string>();
        try {
            foreach (var app in _apps) {
                if (await TryCheckAsync(app.Name) is { } failure) failures.Add(failure);
            }
            await LoadAgentAsync();
        } finally {
            _busy = false;
        }

        if (failures.Count == 0) Toasts.Push(StatusNoteKind.Ok, $"Checked {_apps.Count} app(s).");
        else Toasts.Push(StatusNoteKind.Error, string.Join("; ", failures));
    }

    private async Task<string?> TryCheckAsync(string app) {
        try {
            await Client.CheckAsync(app, CancellationToken.None);
            return null;
        } catch (Exception e) when (IsAgentFailure(e)) {
            return $"{app}: {e.Message}";
        }
    }

    private async Task RedeployAsync(string stack) {
        if (_busy) return;
        if (!IsArmed(stack)) {
            _armedStack = stack;
            _armedAt = DateTimeOffset.UtcNow;
            return;
        }

        _armedStack = null;
        _busy = true;
        try {
            var failure = await Client.RedeployStackAsync(stack, CancellationToken.None);
            if (failure is null) Toasts.Push(StatusNoteKind.Ok, $"Redeploy of {stack} requested.");
            else Toasts.Push(StatusNoteKind.Error, failure);
            await LoadAgentAsync();
        } catch (Exception e) when (IsAgentFailure(e)) {
            Toasts.Push(StatusNoteKind.Error, e.Message);
        } finally {
            _busy = false;
        }
    }

    private static bool IsAgentFailure(Exception e) =>
        e is HttpRequestException or TimeoutException or OperationCanceledException;
}
