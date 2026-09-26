using EggIdentity.Bot;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Settings.Store;
using EggIdentity.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Suite.AdminUi;

public sealed partial class SuitePanel : ComponentBase {
    private const string OverviewTab = "overview";
    private const string DeployTab = "deploy";
    private const string SettingsTab = "settings";
    private const string BotTab = "bot";
    private const string CloneTab = "clone";
    private const string StacksKey = "\u0000stacks";
    private const string NewKey = "\u0000new";

    private SuiteAdmin? _admin;
    private IReadOnlyList<SuiteAppView>? _views;
    private string? _error;
    private string? _selected;
    private string _tab = OverviewTab;
    private bool _hasFleet;

    [Parameter] public string? LocalApp { get; set; }
    [Parameter] public string IdentityHostApp { get; set; } = "eggidentity";
    [Parameter] public string? UpdatedBy { get; set; }

    private SuiteAppView? Current => _views?.FirstOrDefault(v => string.Equals(v.App.Name, _selected, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<IGrouping<string, SuiteAppView>> Groups =>
        (_views ?? []).GroupBy(v => v.App.IsSubProd ? "Sub-prod" : "Production");

    private List<(string Key, string Label, int? Count)> Tabs(SuiteAppView view) {
        var tabs = new List<(string Key, string Label, int? Count)> { (OverviewTab, "Overview", null) };
        if (_hasFleet && !string.IsNullOrWhiteSpace(view.App.Stack)) tabs.Add((DeployTab, "Deploy", null));
        if (view.Serves(AdminCapabilities.Settings)) tabs.Add((SettingsTab, "Settings", null));
        if (BotAdmin(view) is not null) tabs.Add((BotTab, "Discord", null));
        if (view.App.IsSubProd && view.Serves(AdminCapabilities.Clone)) tabs.Add((CloneTab, "Clone", null));
        return tabs;
    }

    protected override async Task OnInitializedAsync() {
        _admin = Services.GetService<SuiteAdmin>();
        _hasFleet = Services.GetService<FleetClient>() is not null;
        if (_admin is null) return;
        _admin.LocalApp = LocalApp;
        _admin.IdentityHostApp = IdentityHostApp;
        await LoadAsync();
    }

    private async Task LoadAsync() {
        if (_admin is null) return;
        try {
            var apps = await _admin.AppsAsync();
            var views = await Task.WhenAll(apps.Select(a => _admin.DescribeAsync(a)));
            _views = views;
            _error = null;
            _selected ??= views.Length > 0 ? views[0].App.Name : NewKey;
        } catch (Exception e) {
            _views = [];
            _error = e.Message;
        }
    }

    private void Select(string key) {
        _selected = key;
        _tab = OverviewTab;
    }

    private async Task OnSavedAsync(string name) {
        await LoadAsync();
        _selected = name;
    }

    private async Task OnDeletedAsync() {
        _selected = null;
        await LoadAsync();
    }

    private ISettingsAdmin? SettingsAdmin(SuiteAppView view) => _admin?.SettingsFor(view);

    private IBotConfigAdmin? BotAdmin(SuiteAppView view) => _admin?.BotFor(view, _views ?? []);

    private static string StatusText(SuiteAppView view) {
        if (!view.App.Enabled) return "disabled";
        if (view.IsLocal) return "this app";
        if (view.Manifest is { } manifest) return string.IsNullOrEmpty(manifest.Version) ? "reachable" : manifest.Version;
        return view.ManifestError ?? "not administrable";
    }

    private static WorkbenchTone Tone(SuiteAppView view) {
        if (!view.App.Enabled) return WorkbenchTone.Muted;
        if (view.IsLocal || view.Manifest is not null) return SuiteApps.Problems(view.App).Count > 0 ? WorkbenchTone.Warn : WorkbenchTone.Normal;
        return string.IsNullOrWhiteSpace(view.App.AdminBaseUrl) ? WorkbenchTone.Normal : WorkbenchTone.Bad;
    }
}
