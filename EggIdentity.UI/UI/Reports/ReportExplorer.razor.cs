using EggIdentity.Reports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace EggIdentity.UI.Reports;

public sealed partial class ReportExplorer : ComponentBase, IDisposable {
    [Parameter, EditorRequired]
    public IReadOnlyList<IReportSource> Sources { get; set; } = [];

    [Parameter, EditorRequired]
    public ReportEngineRegistry Registry { get; set; } = null!;

    [Parameter, EditorRequired]
    public ReportDefinition Value { get; set; } = null!;

    [Parameter]
    public EventCallback<ReportDefinition> ValueChanged { get; set; }

    [Parameter]
    public IReadOnlyList<ReportPreset>? Presets { get; set; }

    [Parameter]
    public ReportTier Tier { get; set; } = ReportTier.Live;

    [Parameter]
    public TimeZoneInfo Zone { get; set; } = TimeZoneInfo.Utc;

    [Parameter]
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(300);

    [Parameter]
    public bool ShowBuilder { get; set; } = true;

    [Parameter]
    public IReportCostEstimator? Cost { get; set; }

    [Parameter]
    public ChartStyle? Style { get; set; }

    [Parameter]
    public ReportLabels Labels { get; set; } = ReportLabels.Default;

    [Parameter]
    public ReportBuilderLabels BuilderLabels { get; set; } = ReportBuilderLabels.Default;

    [Parameter]
    public ReportRenderers? Renderers { get; set; }

    private ReportCube? Cube { get; set; }
    private bool Loading { get; set; }
    private string? Error { get; set; }

    private bool Failed => Error is not null;
    private ReportQuery? _lastQuery;
    private ReportTier _lastTier;
    private bool _firstRun = true;
    private CancellationTokenSource? _cts;

    private IReadOnlyList<ReportPreset> RunnablePresets { get; set; } = [];

    private IReportSource? CurrentSource {
        get {
            for (var i = 0; i < Sources.Count; i++) {
                if (string.Equals(Sources[i].Id, Value.Query.Source, StringComparison.Ordinal)) {
                    return Sources[i];
                }
            }
            return null;
        }
    }

    [Inject] private ILogger<ReportExplorer> Logger { get; set; } = null!;

    protected override void OnParametersSet() {
        var presetSource = Presets ?? Sources.SelectMany(static s => s.Presets);
        RunnablePresets = ReportPresets.Runnable(presetSource, Tier, Registry);
        if (_firstRun) return;
        var queryChanged = !ReferenceEquals(_lastQuery, Value.Query) && _lastQuery != Value.Query;
        if (queryChanged || _lastTier != Tier) {
            _lastQuery = Value.Query;
            _lastTier = Tier;
            Schedule(Value, Debounce);
        }
    }

    protected override void OnAfterRender(bool firstRender) {
        if (!firstRender) return;
        _firstRun = false;
        _lastQuery = Value.Query;
        _lastTier = Tier;
        Schedule(Value, TimeSpan.Zero);
    }

    private async Task OnValueChanged(ReportDefinition def) {
        ArgumentNullException.ThrowIfNull(def);
        await ValueChanged.InvokeAsync(def);
        Schedule(def, Debounce);
    }

    private void Schedule(ReportDefinition def, TimeSpan delay) {
        _cts?.Cancel();
        _cts?.Dispose();
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = RunAsync(def, delay, cts.Token);
    }

    private async Task RunAsync(ReportDefinition def, TimeSpan delay, CancellationToken token) {
        try {
            if (delay > TimeSpan.Zero) {
                await Task.Delay(delay, token);
            }
            if (string.IsNullOrEmpty(def.Query.Source)) {
                return;
            }
            Loading = true;
            Error = null;
            await InvokeAsync(StateHasChanged);
            Cube = await Registry.RunAsync(def.Query, new ReportContext(Zone, Tier), token);
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            return;
        } catch (ReportRejectedException e) {
            Error = string.Join(" ", e.Rejections.Select(r => r.Message));
            Cube = null;
        } catch (Exception e) when (e is KeyNotFoundException or ArgumentException) {
            Error = e.Message;
            Cube = null;
        } catch (Exception e) {
            Logger.LogError(e, "report run failed for source {Source}", def.Query.Source);
            Error = Labels.Failed;
            Cube = null;
        } finally {
            if (!token.IsCancellationRequested) {
                Loading = false;
                await Repaint();
            }
        }
    }

    private async Task Repaint() {
        try {
            await InvokeAsync(StateHasChanged);
        } catch (ObjectDisposedException) {
            Loading = false;
        }
    }

    public void Dispose() {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
