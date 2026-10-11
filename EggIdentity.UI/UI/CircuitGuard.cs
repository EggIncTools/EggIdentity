using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EggIdentity.UI;

public sealed class CircuitGuard : ErrorBoundaryBase {
    [Inject] private IServiceProvider Services { get; set; } = null!;

    [Parameter] public string? Name { get; set; }

    protected override Task OnErrorAsync(Exception exception) {
        Services.GetService<ILogger<CircuitGuard>>()?.LogError(exception, "{Component} faulted and was contained", Name ?? nameof(CircuitGuard));
        return Task.CompletedTask;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) {
        if (CurrentException is null) builder.AddContent(0, ChildContent);
        else builder.AddContent(1, ErrorContent, CurrentException);
    }
}
