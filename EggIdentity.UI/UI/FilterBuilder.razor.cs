using EggIdentity.Filters;
using Microsoft.AspNetCore.Components;

namespace EggIdentity.UI;

public sealed partial class FilterBuilder : ComponentBase {
    private FilterDraft _own = new();
    private FilterSet? _applied;
    private FilterDraftState? _lastState;

    [Parameter, EditorRequired] public IReadOnlyList<FilterFieldDef> Fields { get; set; } = [];
    [Parameter] public FilterSet Value { get; set; } = FilterSet.Empty;
    [Parameter] public EventCallback<FilterSet> ValueChanged { get; set; }
    [Parameter] public FilterDraft? Draft { get; set; }
    [Parameter] public EventCallback<FilterDraftState> ValidityChanged { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public Func<FilterFieldDef, IReadOnlyList<FilterOpDef>>? OpsFor { get; set; }
    [Parameter] public Func<FilterFieldDef, IReadOnlyList<FilterOption>>? OptionsFor { get; set; }
    [Parameter] public int OptionsVersion { get; set; }
    [Parameter] public RenderFragment<FilterValueEditorContext>? ValueEditor { get; set; }
    [Parameter] public Func<FilterFieldDef, bool>? ValueEditorFor { get; set; }
    [Parameter] public RenderFragment<FilterOption>? OptionTemplate { get; set; }
    [Parameter] public FilterBuilderLabels Labels { get; set; } = FilterBuilderLabels.Default;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    public FilterDraft CurrentDraft => Draft ?? _own;

    public FilterSet Current => CurrentDraft.Complete(Fields, OpsFor);

    protected override async Task OnParametersSetAsync() {
        if (Draft is null && !ReferenceEquals(Value, _applied)) {
            _applied = Value;
            _own = new FilterDraft();
            _own.Hydrate(Value);
        }
        CurrentDraft.EnsureTrailingRow();
        await ReportValidityAsync();
    }

    private FilterFieldDef? Field(string key) => key.Length == 0 ? null : FilterFields.Find(Fields, key);

    private IReadOnlyList<FilterOpDef> Ops(FilterFieldDef field) => OpsFor?.Invoke(field) ?? field.Ops;

    private IReadOnlyList<FilterOption> Options(FilterFieldDef field) => OptionsFor?.Invoke(field) ?? field.Options;

    private bool UsesEditor(FilterFieldDef field) => ValueEditor is not null && (ValueEditorFor?.Invoke(field) ?? false);

    private string? Problem(FilterCondition cond) => FilterValidator.Check(cond, Fields, OpsFor);

    private static bool IsBool(FilterFieldDef field) => field.Kind == FilterValueKind.Bool;

    private string OpOf(FilterCondition cond, FilterFieldDef field) {
        var ops = Ops(field);
        return cond.Op.Length == 0 && ops.Count == 1 ? ops[0].Value : FilterOps.Canonical(cond.Op);
    }

    private bool OrEnabled(FilterDraftRow row) =>
        Problem(row.Head) is null && !FilterValidator.IsBlank(row.Head)
        && (row.Or.Count == 0 || (!FilterValidator.IsBlank(row.Or[^1]) && Problem(row.Or[^1]) is null));

    private bool ShowOr(FilterDraftRow row) => !FilterValidator.IsBlank(row.Head) && Problem(row.Head) is null;

    private FilterCondition Get(int row, int member) => member == 0 ? CurrentDraft.Rows[row].Head : CurrentDraft.Rows[row].Or[member - 1];

    private void Put(int row, int member, FilterCondition cond) {
        if (member == 0) CurrentDraft.Rows[row].Head = cond;
        else CurrentDraft.Rows[row].Or[member - 1] = cond;
    }

    private Task SetField(int row, int member, string? key) {
        Put(row, member, new FilterCondition(key ?? "", InitialOp(Field(key ?? "")), ""));
        return ChangedAsync();
    }

    private string InitialOp(FilterFieldDef? field) {
        if (field is null) return "";
        if (IsBool(field)) return FilterOps.True;
        var ops = Ops(field);
        return ops.Count == 1 ? ops[0].Value : "";
    }

    private Task SetOp(int row, int member, string? op) {
        Put(row, member, Get(row, member) with { Op = op ?? "" });
        return ChangedAsync();
    }

    private Task SetValue(int row, int member, string? value) {
        Put(row, member, Get(row, member) with { Value = value ?? "" });
        return ChangedAsync();
    }

    private void AddOr(int row) => CurrentDraft.Rows[row].Or.Add(FilterDraft.Blank);

    private Task Remove(int row, int member) {
        CurrentDraft.Remove(row, member);
        return ChangedAsync();
    }

    private async Task ChangedAsync() {
        CurrentDraft.EnsureTrailingRow();
        _applied = CurrentDraft.Complete(Fields, OpsFor);
        await ValueChanged.InvokeAsync(_applied);
        await ReportValidityAsync();
    }

    private async Task ReportValidityAsync() {
        var state = CurrentDraft.State(Fields, OpsFor);
        if (_lastState is { } last && last.IsValid == state.IsValid && last.Problems.SequenceEqual(state.Problems)) return;
        _lastState = state;
        await ValidityChanged.InvokeAsync(state);
    }

    private IReadOnlyList<ComboOption> Combo(FilterFieldDef field) =>
        [.. Options(field).Select(o => new ComboOption(o.Value, o.Label) { Group = o.Group, Badge = o.Badge, Image = o.Image, Class = o.Class })];

    private RenderFragment<ComboOption>? ComboTemplate() =>
        OptionTemplate is { } template
            ? c => template(new FilterOption(c.Value, c.Label) { Group = c.Group, Badge = c.Badge, Image = c.Image, Class = c.Class })
            : null;

    private string OptionLabel(FilterFieldDef field, string value) =>
        Options(field).FirstOrDefault(o => o.Value == value)?.Label ?? value;

    private string OpLabel(FilterFieldDef field, string op) =>
        Ops(field).FirstOrDefault(o => o.Value == FilterOps.Canonical(op))?.Label ?? op;

    private static string InputType(FilterFieldDef field) => field.Kind switch {
        FilterValueKind.Number => "number",
        FilterValueKind.Date => "date",
        _ => "text",
    };

    private static string Css(string baseClass, bool incomplete) => incomplete ? $"{baseClass} fb-incomplete" : baseClass;
}
