using EggIdentity.Deploy;
using EggIdentity.Settings;
using Microsoft.AspNetCore.Components;

namespace EggIdentity.Suite.AdminUi;

public sealed partial class SuiteAppEditor : ComponentBase {
    private const string Mask = "********";
    private static readonly string[] GroupOrder =
        [SuiteApps.IdentityGroup, SuiteApps.RuntimeGroup, SuiteApps.AdminGroup, SuiteApps.LoginGroup, SuiteApps.DiscordGroup];

    private Dictionary<string, string?> _values = [with(StringComparer.Ordinal)];
    private readonly HashSet<string> _dirty = [with(StringComparer.Ordinal)];
    private string? _error;
    private string? _note;
    private bool _busy;
    private bool _confirmDelete;
    private bool _showHelp;
    private string? _loadedFor;

    [Parameter, EditorRequired] public SuiteAdmin Admin { get; set; } = null!;
    [Parameter] public string? Name { get; set; }
    [Parameter] public string? UpdatedBy { get; set; }
    [Parameter] public EventCallback<string> OnSaved { get; set; }
    [Parameter] public EventCallback OnDeleted { get; set; }

    private bool IsNew => string.IsNullOrEmpty(Name);

    private SuiteApp Bound => CollectionBinder.Bind<SuiteApp>(_values);

    private IReadOnlyList<string> Problems => SuiteApps.Problems(Bound);

    private IEnumerable<IGrouping<string, FieldDescriptor>> Groups =>
        SuiteApps.Descriptor.Fields
            .Where(f => f.IsVisible(_values))
            .GroupBy(f => f.Group ?? SuiteApps.IdentityGroup)
            .OrderBy(g => Array.IndexOf(GroupOrder, g.Key));

    protected override async Task OnParametersSetAsync() {
        var key = Name ?? "";
        if (_loadedFor == key) return;
        _loadedFor = key;
        _dirty.Clear();
        _error = null;
        _note = null;
        _confirmDelete = false;
        _values = IsNew
            ? SuiteApps.Descriptor.Fields.ToDictionary(f => f.Name, f => f.Default, StringComparer.Ordinal)
            : new Dictionary<string, string?>(await Admin.RowAsync(key), StringComparer.Ordinal);
    }

    private string Value(FieldDescriptor field) => _values.GetValueOrDefault(field.Name) ?? "";

    private bool IsOn(FieldDescriptor field) => string.Equals(Value(field), "true", StringComparison.OrdinalIgnoreCase);

    private void SetBool(FieldDescriptor field, object? state) => Set(field, state is true ? "true" : "false");

    private void Set(FieldDescriptor field, string? value) {
        _values[field.Name] = string.IsNullOrEmpty(value) ? null : value;
        _dirty.Add(field.Name);
        _note = null;
    }

    private bool Locked(FieldDescriptor field) => !IsNew && field.Name == SuiteApps.Descriptor.IdField;

    private static string FieldId(FieldDescriptor field) => "suite-field-" + field.Name;

    private static string InputType(FieldDescriptor field) => field.IsSecret ? "password" : "text";

    private string Placeholder(FieldDescriptor field) =>
        field.IsSecret && Value(field) == Mask ? "set, leave blank to keep" : field.Default ?? "not set";

    private async Task SaveAsync() {
        if (_busy) return;
        var name = _values.GetValueOrDefault("name")?.Trim();
        if (string.IsNullOrEmpty(name)) {
            _error = "App name is required.";
            return;
        }
        _busy = true;
        _error = null;
        try {
            var result = await Admin.SaveAsync(name, _values, IsNew, UpdatedBy);
            if (!result.Ok) {
                _error = result.Error;
                return;
            }
            _dirty.Clear();
            _note = "Saved.";
            await OnSaved.InvokeAsync(name);
        } finally {
            _busy = false;
        }
    }

    private async Task DeleteAsync() {
        if (_busy || IsNew || Name is null) return;
        if (!_confirmDelete) {
            _confirmDelete = true;
            return;
        }
        _busy = true;
        try {
            var result = await Admin.DeleteAsync(Name);
            if (!result.Ok) {
                _error = result.Error;
                return;
            }
            await OnDeleted.InvokeAsync();
        } finally {
            _busy = false;
            _confirmDelete = false;
        }
    }
}
