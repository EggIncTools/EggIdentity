using EggIdentity.Contract;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EggIdentity.UI;

public sealed class CircuitGuard : ErrorBoundaryBase {
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromSeconds(10);

    private string? _faultId;
    private string? _detail;
    private DateTimeOffset? _retriedAt;
    private bool _repeat;

    [Inject] private IServiceProvider Services { get; set; } = null!;

    [Parameter] public string? Name { get; set; }
    [Parameter] public FaultScope Scope { get; set; } = FaultScope.Panel;
    [Parameter] public string? Title { get; set; }
    [Parameter] public EventCallback OnRecover { get; set; }

    private TimeProvider Clock => Services.GetService<TimeProvider>() ?? TimeProvider.System;

    protected override async Task OnErrorAsync(Exception exception) {
        _faultId = FaultId.New();
        _repeat = _retriedAt is { } at && Clock.GetUtcNow() - at <= RepeatWindow;
        _detail = await CanSeeDetailAsync() ? $"{Name ?? nameof(CircuitGuard)}: {exception}" : null;
        var logger = Services.GetService<ILogger<CircuitGuard>>();
        if (logger is null) return;
        using (logger.BeginScope(new Dictionary<string, object?> {
            ["FaultId"] = _faultId,
            ["TraceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString(),
        })) {
            logger.LogError(exception, "{Component} faulted [{FaultId}]", Name ?? nameof(CircuitGuard), _faultId);
        }
    }

    private async Task<bool> CanSeeDetailAsync() {
        if (Services.GetService<IFaultDetailGate>() is not { } gate) return false;
        try {
            return await gate.CanSeeDetailAsync();
        } catch (Exception e) when (e is not OperationCanceledException) {
            return false;
        }
    }

    private async Task RetryAsync() {
        _retriedAt = Clock.GetUtcNow();
        await OnRecover.InvokeAsync();
        Recover();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) {
        if (CurrentException is null) {
            builder.AddContent(0, ChildContent);
            return;
        }
        if (ErrorContent is not null) {
            builder.AddContent(1, ErrorContent, CurrentException);
            return;
        }
        builder.OpenComponent<FaultView>(2);
        builder.AddComponentParameter(3, nameof(FaultView.Scope), Scope);
        builder.AddComponentParameter(4, nameof(FaultView.Kind), _repeat ? FaultKind.CrashRepeat : FaultKind.Crash);
        builder.AddComponentParameter(5, nameof(FaultView.Title), Title);
        builder.AddComponentParameter(6, nameof(FaultView.FaultId), _faultId);
        builder.AddComponentParameter(7, nameof(FaultView.Detail), _detail);
        builder.AddComponentParameter(8, nameof(FaultView.OnRetry), EventCallback.Factory.Create(this, RetryAsync));
        builder.CloseComponent();
    }
}
