using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Deploy.AdminUi;

public sealed partial class PromotionPanel : ComponentBase {
    private const string PromoteAction = "promote";
    private const string RollBackAction = "rollback";
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);

    private PromotionService? _service;
    private IReadOnlyList<PromotionView>? _views;
    private string? _error;
    private string? _note;
    private bool _busy;
    private string? _pendingAction;
    private string? _pendingApp;
    private DateTimeOffset _pendingAt;

    [Parameter] public string? UpdatedBy { get; set; }

    protected override async Task OnInitializedAsync() {
        _service = Services.GetService<PromotionService>();
        if (_service is null) return;
        await LoadAsync(_service);
    }

    private bool IsConfirming(string action, string app) =>
        string.Equals(_pendingAction, action, StringComparison.Ordinal)
        && string.Equals(_pendingApp, app, StringComparison.Ordinal)
        && DateTimeOffset.UtcNow - _pendingAt < ConfirmWindow;

    private bool Arm(string action, string app) {
        if (IsConfirming(action, app)) {
            _pendingAction = null;
            _pendingApp = null;
            return true;
        }
        _pendingAction = action;
        _pendingApp = app;
        _pendingAt = DateTimeOffset.UtcNow;
        _note = null;
        _error = null;
        return false;
    }

    private Task PromoteAsync(string app) =>
        Arm(PromoteAction, app)
            ? RunAsync((service, ct) => service.PromoteAsync(app, UpdatedBy, ct), $"{app} promoted.")
            : Task.CompletedTask;

    private Task RollBackAsync(string app) =>
        Arm(RollBackAction, app)
            ? RunAsync((service, ct) => service.RollBackAsync(app, UpdatedBy, ct), $"{app} rolled back.")
            : Task.CompletedTask;

    private async Task RunAsync(Func<PromotionService, CancellationToken, Task<string?>> work, string success) {
        if (_service is not { } service) return;
        _busy = true;
        _error = null;
        _note = null;
        try {
            var failure = await work(service, CancellationToken.None);
            if (failure is not null) {
                _error = failure;
                return;
            }
            _note = success;
            await LoadAsync(service);
        } catch (Exception e) {
            _error = e.Message;
        } finally {
            _busy = false;
        }
    }

    private async Task LoadAsync(PromotionService service) {
        try {
            _views = await service.ViewAsync(CancellationToken.None);
        } catch (Exception e) {
            _views = [];
            _error = e.Message;
        }
    }
}
