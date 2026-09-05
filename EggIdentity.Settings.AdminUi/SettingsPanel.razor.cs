using EggIdentity.Settings.Store;
using EggIdentity.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace EggIdentity.Settings.AdminUi;

public sealed partial class SettingsPanel : IDisposable {
    internal enum PaneKind {
        Category,
        Collection,
        Drift,
        Stack,
    }

    internal sealed record Pane(PaneKind Kind, string? Key);

    internal sealed record ControlSpec(
        string Id,
        SettingKind Kind,
        IReadOnlyList<string> EnumValues,
        string Value,
        Action<string?> Set,
        bool Disabled,
        string? Placeholder,
        bool Password);

    internal sealed class StackEdit {
        public required string Key { get; init; }
        public bool Remove { get; init; }
        public string Value { get; set; } = "";
        public string? Error { get; set; }
        public bool Applied { get; set; }
    }

    internal sealed class RowEdit {
        public required CollectionDescriptor Descriptor { get; init; }
        public bool IsNew { get; init; }
        public Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);
        public HashSet<string> MaskedFields { get; } = new(StringComparer.Ordinal);
        public string? Error { get; set; }
    }

    private static readonly TimeSpan DeleteConfirmWindow = TimeSpan.FromSeconds(5);

    internal static readonly IReadOnlyDictionary<string, string> Icons = new Dictionary<string, string>(StringComparer.Ordinal) {
        ["lock"] = "<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3\" y=\"11\" width=\"18\" height=\"11\" rx=\"2\"/><path d=\"M7 11V7a5 5 0 0 1 10 0v4\"/></svg>",
        ["copy"] = "<svg width=\"14\" height=\"14\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"9\" y=\"9\" width=\"13\" height=\"13\" rx=\"2\"/><path d=\"M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1\"/></svg>",
    };

    [Inject] private IServiceProvider Services { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public string? UpdatedBy { get; set; }
    [Parameter] public string? InitialSelection { get; set; }

    private SettingsAdminService? _admin;
    private IEnvSource? _envSource;
    private IRestartTrigger? _restart;
    private IStackEnvEditor? _stackEditor;
    private ToastService? _toasts;

    private IReadOnlyList<SettingRow>? _rows;
    private string? _rowsError;
    private IReadOnlyList<CollectionDescriptor> _collections = [];
    private readonly Dictionary<string, IReadOnlyList<CollectionRow>?> _collectionRows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _collectionErrors = new(StringComparer.Ordinal);

    private IReadOnlyList<EnvKeyInfo>? _env;
    private DriftReport? _drift;
    private string? _envError;

    private readonly Dictionary<string, string?> _drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _rowErrors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _revealed = new(StringComparer.Ordinal);

    private Pane _pane = new(PaneKind.Category, null);
    private string _query = "";
    private string? _highlightKey;
    private bool _showMatched;

    private bool _busy;
    private string? _status;
    private StatusNoteKind _statusKind;

    private StackEdit? _stackEdit;
    private RowEdit? _rowEdit;
    private string? _confirmDeleteId;
    private DateTimeOffset _confirmDeleteAt;
    private readonly CancellationTokenSource _disposeCts = new();

    protected override async Task OnInitializedAsync() {
        _admin = Services.GetService<SettingsAdminService>();
        _envSource = Services.GetService<IEnvSource>();
        _restart = Services.GetService<IRestartTrigger>();
        _stackEditor = Services.GetService<IStackEnvEditor>();
        _toasts = Services.GetService<ToastService>();
        if (_admin is null) return;

        _collections = _admin.Collections;
        await LoadRowsAsync();
        _pane = ResolveInitialPane();
        await Task.WhenAll(LoadAllCollectionsAsync(), LoadEnvAsync());
    }

    private Pane ResolveInitialPane() {
        var wanted = InitialSelection;
        if (wanted is { Length: > 0 }) {
            if (Categories.Contains(wanted, StringComparer.Ordinal)) return new Pane(PaneKind.Category, wanted);
            if (_collections.Any(c => string.Equals(c.Key, wanted, StringComparison.Ordinal))) return new Pane(PaneKind.Collection, wanted);
        }
        var categories = Categories;
        if (categories.Count > 0) return new Pane(PaneKind.Category, categories[0]);
        if (_collections.Count > 0) return new Pane(PaneKind.Collection, _collections[0].Key);
        return new Pane(PaneKind.Drift, null);
    }

    private IReadOnlyList<string> Categories =>
        _rows is null ? [] : [.. _rows.Select(r => r.Descriptor.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    private bool HasQuery => _query.Trim().Length > 0;

    private bool IsSelected(PaneKind kind, string? key = null) =>
        !HasQuery && _pane.Kind == kind && string.Equals(_pane.Key, key, StringComparison.Ordinal);

    private void Select(PaneKind kind, string? key = null) {
        _pane = new Pane(kind, key);
        _query = "";
        _highlightKey = null;
    }

    private void OnQueryChanged(string value) {
        _query = value;
        _highlightKey = null;
    }

    private IReadOnlyCollection<SettingRow>? RowsIn(string category) =>
        _rows is null ? null : [.. _rows.Where(r => string.Equals(r.Descriptor.Category, category, StringComparison.Ordinal))];

    private int RowCount(string category) => RowsIn(category)?.Count ?? 0;

    private bool HasPendingRestart(string category) =>
        _rows?.Any(r => r.PendingRestart && string.Equals(r.Descriptor.Category, category, StringComparison.Ordinal)) == true;

    private bool HasPendingRestartFor(string collectionKey) =>
        _admin?.PendingRestartKeys.Contains(collectionKey, StringComparer.Ordinal) == true;

    private int DirtyCount(string? category = null) =>
        _rows?.Count(r => IsDirty(r) && (category is null || string.Equals(r.Descriptor.Category, category, StringComparison.Ordinal))) ?? 0;

    private IEnumerable<IGrouping<string, SettingRow>> SearchResults() {
        if (_rows is null) return [];
        var q = _query.Trim();
        return _rows
            .Where(r => Matches(r.Descriptor, q))
            .GroupBy(r => r.Descriptor.Category, StringComparer.Ordinal);
    }

    private static bool Matches(SettingDescriptor d, string q) =>
        d.Label.Contains(q, StringComparison.OrdinalIgnoreCase)
        || d.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
        || d.EnvKey.Contains(q, StringComparison.OrdinalIgnoreCase);

    private async Task LoadRowsAsync() {
        if (_admin is null) return;
        _rowsError = null;
        try {
            _rows = await _admin.GetRowsAsync(CancellationToken.None);
        } catch (Exception e) {
            _rowsError = e.Message;
        }
    }

    private async Task LoadAllCollectionsAsync() {
        foreach (var c in _collections) await LoadCollectionAsync(c.Key);
    }

    private async Task LoadCollectionAsync(string key) {
        if (_admin is null) return;
        _collectionErrors.Remove(key);
        try {
            _collectionRows[key] = await _admin.GetRowsAsync(key, CancellationToken.None);
        } catch (Exception e) {
            _collectionRows[key] = [];
            _collectionErrors[key] = e.Message;
        }
    }

    private async Task LoadEnvAsync() {
        if (_envSource is null || _admin is null) return;
        _envError = null;
        try {
            _env = await _envSource.GetAsync(CancellationToken.None);
            _drift = await _admin.DriftAsync(_env, CancellationToken.None);
        } catch (Exception e) {
            _envError = e.Message;
        }
    }

    private async Task ReloadAsync() {
        _busy = true;
        try {
            await LoadRowsAsync();
            await Task.WhenAll(LoadAllCollectionsAsync(), LoadEnvAsync());
        } finally {
            _busy = false;
        }
    }

    private IReadOnlyCollection<CollectionRow>? CollectionRows(string key) =>
        _collectionRows.GetValueOrDefault(key);

    private int CollectionCount(string key) => CollectionRows(key)?.Count ?? 0;

    private CollectionDescriptor? SelectedCollection =>
        _pane.Kind == PaneKind.Collection
            ? _collections.FirstOrDefault(c => string.Equals(c.Key, _pane.Key, StringComparison.Ordinal))
            : null;

    private static IReadOnlyList<FieldDescriptor> VisibleFields(CollectionDescriptor descriptor) {
        var display = descriptor.DisplayField ?? descriptor.IdField;
        var first = descriptor.FindField(display);
        var rest = descriptor.Fields.Where(f => !string.Equals(f.Name, display, StringComparison.Ordinal));
        return first is null ? [.. rest] : [first, .. rest];
    }

    private static bool IsSecret(SettingDescriptor d) => d.IsSecret || d.Kind == SettingKind.Secret;

    private static bool IsSecret(FieldDescriptor f) => f.IsSecret || f.Kind == SettingKind.Secret;

    private static bool IsLockedBootstrap(SettingDescriptor d) =>
        d.Tier == ApplyTier.Bootstrap && !d.AllowBootstrapEdit;

    private static bool IsReadOnly(SettingDescriptor d) =>
        d.Kind is SettingKind.ReadOnly or SettingKind.External;

    private static string ToControl(SettingKind kind, string? stored) =>
        kind is SettingKind.StringList or SettingKind.CidrList
            ? string.Join('\n', SettingsFormat.ParseList(stored))
            : stored ?? "";

    private static string? ToStored(SettingKind kind, string? control) {
        if (string.IsNullOrWhiteSpace(control)) return null;
        if (kind is SettingKind.StringList or SettingKind.CidrList) {
            var parts = control.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 0 ? null : string.Join(",", parts);
        }
        return control;
    }

    private string Draft(SettingRow row) {
        if (_drafts.TryGetValue(row.Descriptor.Key, out var draft)) return draft ?? "";
        return IsSecret(row.Descriptor) ? "" : ToControl(row.Descriptor.Kind, row.Display);
    }

    private bool IsDirty(SettingRow row) {
        if (!_drafts.TryGetValue(row.Descriptor.Key, out var draft)) return false;
        if (IsSecret(row.Descriptor)) return !string.IsNullOrEmpty(draft);
        return !string.Equals(draft ?? "", ToControl(row.Descriptor.Kind, row.Display), StringComparison.Ordinal);
    }

    private void SetDraft(SettingRow row, string? value) {
        _drafts[row.Descriptor.Key] = value;
        _rowErrors.Remove(row.Descriptor.Key);
    }

    private bool IsRevealed(SettingRow row) => _revealed.Contains(row.Descriptor.Key);

    private void Reveal(SettingRow row) => _revealed.Add(row.Descriptor.Key);

    private static string ControlId(string key) => "sks-" + key.Replace('.', '-');

    private string RowCss(SettingRow row) {
        var css = "sks-row";
        if (IsDirty(row)) css += " sks-row-dirty";
        if (row.PendingRestart) css += " sks-row-restart";
        if (string.Equals(_highlightKey, row.Descriptor.Key, StringComparison.Ordinal)) css += " sks-row-hl";
        return css;
    }

    private static string? InputMode(SettingKind kind) =>
        kind is SettingKind.Number or SettingKind.Snowflake ? "numeric" : null;

    private static string SourceLabel(SettingSource source) => source switch {
        SettingSource.Database => "db",
        SettingSource.File => "file",
        SettingSource.Environment => "env",
        _ => "default",
    };

    private static string OriginLabel(EnvOrigin origin) => origin switch {
        EnvOrigin.ServiceEnvironment => "compose",
        EnvOrigin.EnvFile => "env_file",
        EnvOrigin.StackVariable => "stack",
        EnvOrigin.Image => "image",
        _ => "runtime",
    };

    private ControlSpec SettingSpec(SettingRow row) {
        var d = row.Descriptor;
        var secret = IsSecret(d);
        return new ControlSpec(
            ControlId(d.Key),
            secret ? SettingKind.Secret : d.Kind,
            d.EnumValues,
            Draft(row),
            v => SetDraft(row, v),
            IsLockedBootstrap(d),
            secret && row.Display is not null ? "set, leave blank to keep" : d.Default,
            secret);
    }

    private static ControlSpec LockedSpec(SettingRow row) {
        var d = row.Descriptor;
        var kind = IsSecret(d) ? SettingKind.Text : d.Kind;
        return new ControlSpec(
            ControlId(d.Key),
            kind,
            d.EnumValues,
            ToControl(kind, row.Display),
            _ => { },
            true,
            d.Default,
            false);
    }

    private ControlSpec FieldSpec(RowEdit edit, FieldDescriptor field) {
        var secret = IsSecret(field);
        var isId = string.Equals(field.Name, edit.Descriptor.IdField, StringComparison.Ordinal);
        return new ControlSpec(
            "sks-field-" + field.Name,
            secret ? SettingKind.Secret : field.Kind,
            field.EnumValues,
            edit.Values.GetValueOrDefault(field.Name) ?? "",
            v => {
                edit.Values[field.Name] = v;
                edit.Error = null;
            },
            isId && !edit.IsNew,
            edit.MaskedFields.Contains(field.Name) ? "set, leave blank to keep" : field.Default,
            secret);
    }

    private void Notify(StatusNoteKind kind, string text) {
        if (_toasts is not null) {
            _toasts.Push(kind, text);
            return;
        }
        _status = text;
        _statusKind = kind;
    }

    private string StatusCss => _statusKind switch {
        StatusNoteKind.Error => "sks-status sks-error",
        StatusNoteKind.Ok => "sks-status sks-ok",
        _ => "sks-status",
    };

    private async Task SaveAllAsync() {
        if (_admin is null || _rows is null) return;
        _busy = true;
        _status = null;
        var saved = 0;
        var failed = 0;
        try {
            foreach (var row in _rows.Where(IsDirty).ToList()) {
                if (await SaveRowAsync(row)) saved++;
                else failed++;
            }
            await LoadRowsAsync();
        } finally {
            _busy = false;
        }
        if (failed == 0) Notify(StatusNoteKind.Ok, $"Saved {saved} setting(s).");
        else Notify(StatusNoteKind.Error, $"{failed} setting(s) failed to save.");
    }

    private async Task<bool> SaveRowAsync(SettingRow row) {
        var key = row.Descriptor.Key;
        try {
            var value = ToStored(row.Descriptor.Kind, _drafts.GetValueOrDefault(key));
            var result = await _admin!.SaveAsync(key, value, UpdatedBy, CancellationToken.None);
            if (!result.Ok) {
                _rowErrors[key] = result.Error ?? "save failed";
                return false;
            }
        } catch (Exception e) {
            _rowErrors[key] = e.Message;
            return false;
        }
        _drafts.Remove(key);
        _revealed.Remove(key);
        _rowErrors.Remove(key);
        return true;
    }

    private void Discard() {
        _drafts.Clear();
        _rowErrors.Clear();
        _revealed.Clear();
        _status = null;
    }

    private async Task RestartAsync() {
        if (_restart is null || _admin is null) return;
        _busy = true;
        try {
            var failure = await _restart.RestartAsync(CancellationToken.None);
            if (failure is not null) {
                Notify(StatusNoteKind.Error, failure);
                return;
            }
            _admin.ClearPendingRestart();
            Notify(StatusNoteKind.Ok, "Restart requested.");
        } catch (Exception e) {
            Notify(StatusNoteKind.Error, e.Message);
        } finally {
            _busy = false;
        }
    }

    private void OpenStackEdit(string key, bool remove) {
        var current = _env?.FirstOrDefault(e => string.Equals(e.Name, key, StringComparison.Ordinal));
        _stackEdit = new StackEdit {
            Key = key,
            Remove = remove,
            Value = current is { Masked: false, Value: not null } ? current.Value : "",
        };
    }

    private void CloseStackEdit() => _stackEdit = null;

    private async Task ApplyStackEditAsync() {
        if (_stackEdit is null || _stackEditor is null) return;
        var edit = _stackEdit;
        _busy = true;
        try {
            var changes = new Dictionary<string, string?>(StringComparer.Ordinal) {
                [edit.Key] = edit.Remove ? null : edit.Value,
            };
            var failure = await _stackEditor.ApplyAsync(changes, CancellationToken.None);
            if (failure is not null) {
                edit.Error = failure;
                return;
            }
            edit.Error = null;
            edit.Applied = true;
            Notify(StatusNoteKind.Ok, edit.Remove ? $"Removed {edit.Key} from the stack." : $"Updated {edit.Key} on the stack.");
            await LoadEnvAsync();
        } catch (Exception e) {
            edit.Error = e.Message;
        } finally {
            _busy = false;
        }
    }

    private async Task RestartFromStackEditAsync() {
        await RestartAsync();
        CloseStackEdit();
    }

    private void OpenAdd(CollectionDescriptor descriptor) {
        var edit = new RowEdit { Descriptor = descriptor, IsNew = true };
        foreach (var f in descriptor.Fields) edit.Values[f.Name] = f.Default;
        _rowEdit = edit;
    }

    private void OpenEdit(CollectionDescriptor descriptor, CollectionRow row) {
        var edit = new RowEdit { Descriptor = descriptor, IsNew = false };
        foreach (var f in descriptor.Fields) {
            var stored = row.Get(f.Name);
            if (IsSecret(f)) {
                edit.Values[f.Name] = "";
                if (!string.IsNullOrEmpty(stored)) edit.MaskedFields.Add(f.Name);
                continue;
            }
            edit.Values[f.Name] = ToControl(f.Kind, stored);
        }
        edit.Values[descriptor.IdField] = row.Id;
        _rowEdit = edit;
    }

    private void CloseRowEdit() => _rowEdit = null;

    private async Task SaveRowEditAsync() {
        if (_rowEdit is null || _admin is null) return;
        var edit = _rowEdit;
        var descriptor = edit.Descriptor;
        var id = edit.Values.GetValueOrDefault(descriptor.IdField)?.Trim() ?? "";
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var f in descriptor.Fields) {
            values[f.Name] = IsSecret(f) ? edit.Values.GetValueOrDefault(f.Name) : ToStored(f.Kind, edit.Values.GetValueOrDefault(f.Name));
        }
        _busy = true;
        try {
            var result = edit.IsNew
                ? await _admin.CreateRowAsync(descriptor.Key, id, values, UpdatedBy, CancellationToken.None)
                : await _admin.SaveRowAsync(descriptor.Key, id, values, UpdatedBy, CancellationToken.None);
            if (!result.Ok) {
                edit.Error = result.Error ?? "save failed";
                return;
            }
            _rowEdit = null;
            await LoadCollectionAsync(descriptor.Key);
            Notify(StatusNoteKind.Ok, edit.IsNew ? $"Added {descriptor.Label}: {id}." : $"Saved {descriptor.Label}: {id}.");
        } catch (Exception e) {
            edit.Error = e.Message;
        } finally {
            _busy = false;
        }
    }

    private bool IsConfirmingDelete(string id) =>
        string.Equals(_confirmDeleteId, id, StringComparison.Ordinal)
        && DateTimeOffset.UtcNow - _confirmDeleteAt < DeleteConfirmWindow;

    private async Task DeleteAsync(CollectionDescriptor descriptor, string id) {
        if (_admin is null) return;
        if (!IsConfirmingDelete(id)) {
            _confirmDeleteId = id;
            _confirmDeleteAt = DateTimeOffset.UtcNow;
            _ = ExpireDeleteConfirmAsync(id);
            return;
        }
        _confirmDeleteId = null;
        _busy = true;
        try {
            var result = await _admin.DeleteRowAsync(descriptor.Key, id, CancellationToken.None);
            if (!result.Ok) {
                _collectionErrors[descriptor.Key] = result.Error ?? "delete failed";
                return;
            }
            await LoadCollectionAsync(descriptor.Key);
            Notify(StatusNoteKind.Ok, $"Deleted {descriptor.Label}: {id}.");
        } catch (Exception e) {
            _collectionErrors[descriptor.Key] = e.Message;
        } finally {
            _busy = false;
        }
    }

    private async Task ExpireDeleteConfirmAsync(string id) {
        try {
            await Task.Delay(DeleteConfirmWindow, _disposeCts.Token);
            if (!string.Equals(_confirmDeleteId, id, StringComparison.Ordinal)) return;
            _confirmDeleteId = null;
            await InvokeAsync(StateHasChanged);
        } catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException) {
        }
    }

    public void Dispose() {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    private string DriftSummary() {
        if (_drift is null) return "";
        var parts = new List<string> {
            $"{_drift.Matched.Count} matched",
            $"{_drift.External.Count} external",
            $"{_drift.MissingOptional.Count} optional not set",
        };
        if (!_drift.IsClean) parts.Insert(0, $"{_drift.ProblemCount} problem(s)");
        return string.Join(", ", parts);
    }

    private IReadOnlyList<EnvKeyInfo> SortedEnv =>
        _env is null ? [] : [.. _env.OrderBy(e => e.Name, StringComparer.Ordinal)];

    private static string EnvValueText(EnvKeyInfo info) {
        if (info.Masked) return SettingsAdminService.SecretMask;
        return info.Value ?? "";
    }

    private void GoToSetting(string envKey) {
        var row = _rows?.FirstOrDefault(r => string.Equals(r.Descriptor.EnvKey, envKey, StringComparison.Ordinal));
        if (row is null) return;
        Select(PaneKind.Category, row.Descriptor.Category);
        _highlightKey = row.Descriptor.Key;
    }

    private async Task CopyDescriptorAsync(string envKey) {
        try {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", DescriptorStub(envKey));
            Notify(StatusNoteKind.Ok, "Descriptor copied.");
        } catch (Exception) {
        }
    }

    internal static string DescriptorStub(string envKey) {
        var words = envKey.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var key = string.Join('.', words.Select(w => w.ToLowerInvariant()));
        var label = string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
        var secret = LooksSecret(envKey);
        var kind = secret ? "SettingKind.Secret" : "SettingKind.Text";
        var sensitivity = secret ? "Sensitivity.Secret" : "Sensitivity.Plain";
        return $"new SettingDescriptor(\"{key}\", \"{envKey}\", \"{label}\", \"General\", {kind}, ApplyTier.Live, {sensitivity})";
    }

    private static bool LooksSecret(string envKey) =>
        envKey.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
        || envKey.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
        || envKey.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)
        || envKey.Contains("KEY", StringComparison.OrdinalIgnoreCase);
}
