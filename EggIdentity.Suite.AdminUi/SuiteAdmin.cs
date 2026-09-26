using EggIdentity.Bot;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Deploy.AdminUi;
using EggIdentity.Settings;
using EggIdentity.Settings.Api;
using EggIdentity.Settings.Store;

namespace EggIdentity.Suite.AdminUi;

public sealed record SuiteAppView(SuiteApp App, AdminTargetStatus Target, AdminManifest? Manifest, string? ManifestError) {
    public bool IsLocal { get; init; }

    public bool Serves(string capability) => IsLocal || Manifest?.Has(capability) == true;
}

public sealed class SuiteAdmin(
    SettingsAdminService settings, AdminApiClient client, IHttpClientFactory? http = null, Func<string, string?>? environment = null) {
    private readonly Func<string, string?> _env = environment ?? Environment.GetEnvironmentVariable;

    public string? LocalApp { get; set; }

    public async Task<IReadOnlyList<SuiteApp>> AppsAsync(CancellationToken ct = default) {
        var rows = await settings.GetRowsAsync(SuiteApps.Key, ct);
        return [.. rows.Select(r => CollectionBinder.Bind<SuiteApp>(r.Values))
            .OrderBy(a => a.IsSubProd)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<IReadOnlyDictionary<string, string?>> RowAsync(string name, CancellationToken ct = default) {
        var rows = await settings.GetRowsAsync(SuiteApps.Key, ct);
        return rows.FirstOrDefault(r => string.Equals(r.Id, name, StringComparison.OrdinalIgnoreCase))?.Values
            ?? new Dictionary<string, string?>(StringComparer.Ordinal) { ["name"] = name };
    }

    public async Task<SuiteAppView> DescribeAsync(SuiteApp app, CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(app);
        var target = SuiteAppTargets.Describe(app, _env);
        if (IsLocal(app)) return new SuiteAppView(app, target, null, null) { IsLocal = true };
        if (target.Target is not { } remote) return new SuiteAppView(app, target, null, target.Unavailable);
        try {
            return new SuiteAppView(app, target, await client.GetManifestAsync(remote, ct), null);
        } catch (Exception e) when (e is HttpRequestException or TimeoutException or TaskCanceledException) {
            return new SuiteAppView(app, target, null, e.Message);
        }
    }

    public ISettingsAdmin? SettingsFor(SuiteAppView view) {
        ArgumentNullException.ThrowIfNull(view);
        return view.IsLocal || view.Target.Target is not { } target ? null : new RemoteSettingsAdmin(client, target);
    }

    public string IdentityHostApp { get; set; } = "eggidentity";

    public IBotConfigAdmin? BotFor(SuiteAppView view, IReadOnlyList<SuiteAppView> all) {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(all);
        if (http is null) return null;
        if (view.App.ServedBySuiteBot) {
            var host = all.FirstOrDefault(v => string.Equals(v.App.Name, IdentityHostApp, StringComparison.OrdinalIgnoreCase));
            return host?.Target.Target is { } hub && host.Serves(AdminCapabilities.Bot)
                ? new RemoteBotConfigAdmin(http.CreateClient(), new BotAdminTarget(view.App.Name, hub.BaseUrl, hub.Secret) {
                    Path = $"/admin/api/bot/{Uri.EscapeDataString(view.App.Name)}",
                })
                : null;
        }
        return view.Serves(AdminCapabilities.Bot) && view.Target.Target is { } own
            ? new RemoteBotConfigAdmin(http.CreateClient(), new BotAdminTarget(view.App.Name, own.BaseUrl, own.Secret))
            : null;
    }

    public Task<SettingsSaveResult> SaveAsync(string name, IReadOnlyDictionary<string, string?> values, bool isNew, string? updatedBy, CancellationToken ct = default) =>
        isNew
            ? settings.CreateRowAsync(SuiteApps.Key, name, values, updatedBy, ct)
            : settings.SaveRowAsync(SuiteApps.Key, name, values, updatedBy, ct);

    public Task<SettingsSaveResult> DeleteAsync(string name, CancellationToken ct = default) =>
        settings.DeleteRowAsync(SuiteApps.Key, name, ct);

    private bool IsLocal(SuiteApp app) =>
        LocalApp is { Length: > 0 } local && string.Equals(app.Name, local, StringComparison.OrdinalIgnoreCase);
}
