using EggIdentity.Contract;
using EggIdentity.Deploy;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Suite.AdminUi;

public sealed partial class SuiteStackImport : ComponentBase {
    private FleetClient? _client;
    private IReadOnlyList<StackInfo>? _stacks;
    private string? _stack;
    private string _environment = SuiteApp.ProdEnvironment;
    private List<Row> _rows = [];
    private readonly List<string> _failures = [];
    private string? _error;
    private string? _note;
    private bool _busy;

    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Parameter, EditorRequired] public SuiteAdmin Admin { get; set; } = null!;
    [Parameter] public string? UpdatedBy { get; set; }
    [Parameter] public EventCallback<string?> OnImported { get; set; }

    private sealed class Row(StackImportCandidate candidate) {
        public StackImportCandidate Candidate { get; } = candidate;
        public string Name { get; set; } = candidate.ProposedName;
        public bool Selected { get; set; } = candidate.State == StackImportState.New;
        public bool Importable => Candidate.State != StackImportState.Registered;
    }

    private int SelectedCount => _rows.Count(r => r.Importable && r.Selected);

    protected override async Task OnInitializedAsync() {
        _client = Services.GetService<FleetClient>();
        if (_client is null) return;
        try {
            _stacks = await _client.GetPortainerStacksAsync(CancellationToken.None);
        } catch (Exception e) when (e is HttpRequestException or TimeoutException) {
            _error = e.Message;
        }
    }

    private async Task PickStackAsync(string? stack) {
        _stack = string.IsNullOrEmpty(stack) ? null : stack;
        _rows = [];
        _failures.Clear();
        _error = null;
        _note = null;
        if (_stack is null) return;
        _busy = true;
        try {
            await PlanAsync(_stack);
            if (_rows.Count == 0 && _error is null) _note = "This stack's compose file declares no services.";
        } finally {
            _busy = false;
        }
    }

    private async Task PlanAsync(string stack) {
        if (_client is null) return;
        try {
            var services = await _client.GetStackServicesAsync(stack, CancellationToken.None);
            var apps = await Admin.AppsAsync();
            _rows = [.. StackImport.Plan(stack, services, apps).Select(c => new Row(c))];
        } catch (Exception e) when (e is HttpRequestException or TimeoutException) {
            _error = e.Message;
        }
    }

    private static string StateText(Row row) => row.Candidate.State switch {
        StackImportState.Registered => $"registered as {row.Candidate.ExistingApp}",
        StackImportState.Conflict => "name taken, rename",
        _ => "new",
    };

    private static string StateBadge(Row row) => row.Candidate.State switch {
        StackImportState.Registered => "badge-muted",
        StackImportState.Conflict => "badge-err",
        _ => "badge-ok",
    };

    private async Task ImportAsync() {
        if (_busy || _stack is null) return;
        _busy = true;
        _failures.Clear();
        _error = null;
        _note = null;
        var imported = new List<string>();
        try {
            var apps = await Admin.AppsAsync();
            var chosen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _rows.Where(r => r.Importable && r.Selected)) {
                if (await ImportRowAsync(_stack, row, apps, chosen) is { } name) imported.Add(name);
            }
            _note = imported.Count switch {
                0 => "Nothing imported.",
                1 => "Imported 1 app. Auto deploy is off; turn it on under Runtime.",
                _ => $"Imported {imported.Count} apps. Auto deploy is off on each; turn it on per app under Runtime.",
            };
            await PlanAsync(_stack);
        } finally {
            _busy = false;
        }
        if (imported.Count > 0) await OnImported.InvokeAsync(_failures.Count == 0 ? imported[0] : null);
    }

    private async Task<string?> ImportRowAsync(string stack, Row row, IReadOnlyList<SuiteApp> apps, HashSet<string> chosen) {
        var name = row.Name.Trim();
        var refusal =
            name.Length == 0 ? "name is required"
            : StackImport.NameTaken(name, apps) || !chosen.Add(name) ? "an app with this name already exists"
            : null;
        if (refusal is null) {
            var result = await Admin.SaveAsync(name, StackImport.Values(stack, row.Candidate, name, _environment), true, UpdatedBy);
            if (result.Ok) return name;
            refusal = result.Error;
        }
        _failures.Add($"{(name.Length == 0 ? row.Candidate.Service.Service : name)}: {refusal}");
        return null;
    }
}
