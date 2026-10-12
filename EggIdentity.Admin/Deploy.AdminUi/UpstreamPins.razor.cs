using System.Data.Common;
using System.Text.Json;
using EggIdentity.Contract;
using EggIdentity.Settings.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.Deploy.AdminUi;

public sealed partial class UpstreamPins : ComponentBase, IDisposable {
    private const int BuildLimit = 30;
    private const string MutedBadge = "badge badge-muted";
    private const string PinMessagePrefix = "pin ";
    private const string SessionMissing = "Session forwarding not configured";
    private const string SelfPinWarning = "This app hosts the panel. A broken build here removes the control to roll it back.";
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);

    private FleetClient? _client;
    private IDeployEvents? _events;
    private ISessionTokenSource? _sessions;
    private IReadOnlyList<PinView> _pins = [];
    private readonly Dictionary<string, IReadOnlyList<UpstreamBuildView>> _builds = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly Dictionary<string, string> _buildErrors = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly Dictionary<string, string> _repos = [with(StringComparer.OrdinalIgnoreCase)];
    private readonly CancellationTokenSource _life = new();
    private string? _error;
    private bool _loaded;
    private bool _working;
    private bool _sessionReady;
    private bool _disposed;
    private Armed? _armed;
    private int _epoch;
    private readonly Dictionary<string, int> _rowEpochs = [with(StringComparer.OrdinalIgnoreCase)];

    [Parameter] public string App { get; set; } = "";

    private enum ArmKind { Pick, TakeLatest, Follow }

    private sealed record Armed(string Upstream, ArmKind Kind, string? Version, DateTimeOffset At);

    private bool CanWrite => _sessionReady && !_working;
    private static DateTimeOffset Now => DateTimeOffset.UtcNow;
    private Armed? Current => _armed is { } armed && Now - armed.At <= ConfirmWindow ? armed : null;

    protected override async Task OnInitializedAsync() {
        _client = Services.GetService<FleetClient>();
        _events = Services.GetService<IDeployEvents>();
        _sessions = Services.GetService<ISessionTokenSource>();
        if (_client is null) return;

        _sessionReady = await SessionAsync() is not null;
        if (!_sessionReady) return;
        await LoadReposAsync();
        _events?.Received += OnEvent;
        await LoadAsync();
    }

    private async Task LoadReposAsync() {
        if (Services.GetService<SettingsCache>() is not { } cache) return;
        IReadOnlyList<SuiteUpstream> upstreams;
        try {
            var snapshot = await cache.GetAsync();
            upstreams = snapshot.Collection<SuiteUpstream>(SuiteUpstreams.UpstreamsKey);
        } catch (Exception e) when (e is KeyNotFoundException or DbException or InvalidOperationException or JsonException) {
            return;
        }
        foreach (var upstream in upstreams) {
            if (string.IsNullOrWhiteSpace(upstream.Name) || string.IsNullOrWhiteSpace(upstream.RepoUrl)) continue;
            _repos[upstream.Name.Trim()] = upstream.RepoUrl.Trim().TrimEnd('/');
        }
    }

    private async Task LoadAsync() {
        if (_client is not { } client) return;
        if (await SessionAsync() is not { } session) {
            _sessionReady = false;
            await RenderAsync();
            return;
        }
        try {
            var pins = await client.GetPinsAsync(session, CancellationToken.None);
            _pins = [.. pins.Where(p => Same(p.App, App)).OrderBy(p => p.Upstream, StringComparer.OrdinalIgnoreCase)];
            _error = null;
        } catch (Exception e) when (IsFleetFailure(e)) {
            _error = e.Message;
        }

        foreach (var upstream in _pins.Select(p => p.Upstream).Distinct(StringComparer.OrdinalIgnoreCase)) {
            try {
                _builds[upstream] = await client.GetBuildsAsync(upstream, BuildLimit, session, CancellationToken.None);
                _buildErrors.Remove(upstream);
            } catch (Exception e) when (IsFleetFailure(e)) {
                _buildErrors[upstream] = e.Message;
            }
        }
        _loaded = true;
        await RenderAsync();
    }

    private void OnEvent(DeployEvent evt) {
        if (evt.Phase != DeployPhase.Checked || !Same(evt.App, App)) return;
        if (!evt.Message.StartsWith(PinMessagePrefix, StringComparison.Ordinal)) return;
        _ = FireAsync(LoadAsync);
    }

    private async Task FireAsync(Func<Task> work) {
        try {
            await InvokeAsync(work);
        } catch (Exception e) when (_disposed && e is ObjectDisposedException or OperationCanceledException) {
        } catch (Exception e) when (IsFleetFailure(e)) {
            _error = e.Message;
            await RenderAsync();
        }
    }

    private async Task RenderAsync() {
        try {
            await InvokeAsync(StateHasChanged);
        } catch (ObjectDisposedException) when (_disposed) {
        }
    }

    private async Task<string?> SessionAsync() {
        if (_sessions is null) return null;
        var token = await _sessions.GetAsync(CancellationToken.None);
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private void Arm(PinView pin, ArmKind kind, string? version) {
        if (_armed is { } previous && !Same(previous.Upstream, pin.Upstream)) Bump(previous.Upstream);
        var armed = new Armed(pin.Upstream, kind, version, Now);
        _armed = armed;
        _ = FireAsync(() => ExpireAsync(armed));
    }

    private void Disarm() {
        if (_armed is { } previous) Bump(previous.Upstream);
        _armed = null;
    }

    private void Bump(string upstream) => _rowEpochs[upstream] = _rowEpochs.GetValueOrDefault(upstream) + 1;

    private async Task ExpireAsync(Armed armed) {
        await Task.Delay(ConfirmWindow, _life.Token);
        if (!ReferenceEquals(_armed, armed)) return;
        Disarm();
        StateHasChanged();
    }

    private bool IsArmed(PinView pin, ArmKind kind) => Current is { } armed && armed.Kind == kind && Same(armed.Upstream, pin.Upstream);

    private Armed? TakeArmed(PinView pin, ArmKind kind) {
        if (!IsArmed(pin, kind)) return null;
        var armed = _armed;
        _armed = null;
        return armed;
    }

    private void Pick(PinView pin, ChangeEventArgs args) {
        var value = args.Value as string;
        if (string.IsNullOrWhiteSpace(value) || value == Selected(pin)) {
            Disarm();
            return;
        }
        Arm(pin, ArmKind.Pick, value);
    }

    private Task ConfirmPickAsync(PinView pin) =>
        TakeArmed(pin, ArmKind.Pick) is { Version: { } version }
            ? WriteAsync((client, session) => client.SetPinAsync(pin.App, pin.Upstream, version, pin.FollowLatest, session, CancellationToken.None))
            : Task.CompletedTask;

    private Task TakeLatestAsync(PinView pin) {
        if (TakeArmed(pin, ArmKind.TakeLatest) is { Version: { } captured })
            return WriteAsync((client, session) => client.SetPinAsync(pin.App, pin.Upstream, captured, pin.FollowLatest, session, CancellationToken.None));
        if (pin.Latest is { } latest) Arm(pin, ArmKind.TakeLatest, latest);
        return Task.CompletedTask;
    }

    private Task FollowAsync(PinView pin, ChangeEventArgs args) {
        if (args.Value is true) {
            Arm(pin, ArmKind.Follow, null);
            return Task.CompletedTask;
        }
        if (IsArmed(pin, ArmKind.Follow)) {
            Disarm();
            return Task.CompletedTask;
        }
        return WriteAsync((client, session) => client.SetPinAsync(pin.App, pin.Upstream, pin.Desired, false, session, CancellationToken.None));
    }

    private Task ConfirmFollowAsync(PinView pin) =>
        TakeArmed(pin, ArmKind.Follow) is not null
            ? WriteAsync((client, session) => client.SetPinAsync(pin.App, pin.Upstream, pin.Desired, true, session, CancellationToken.None))
            : Task.CompletedTask;

    private async Task WriteAsync(Func<FleetClient, string, Task<string?>> write) {
        if (_working || _client is not { } client) return;
        _working = true;
        _armed = null;
        string? failure;
        try {
            if (await SessionAsync() is { } session) {
                failure = await write(client, session);
            } else {
                _sessionReady = false;
                failure = SessionMissing;
            }
        } catch (Exception e) when (IsFleetFailure(e)) {
            failure = e.Message;
        } finally {
            _working = false;
        }
        _epoch++;
        await LoadAsync();
        _error = failure ?? _error;
        await RenderAsync();
    }

    private string RowKey(PinView pin) => $"{pin.Upstream}#{_epoch}#{_rowEpochs.GetValueOrDefault(pin.Upstream)}";

    private IReadOnlyList<UpstreamBuildView> BuildsOf(PinView pin) =>
        _builds.TryGetValue(pin.Upstream, out var builds) ? builds : [];

    private string? ShaOf(PinView pin, string? version) =>
        BuildVersion.Parse(version) is { } parsed
            ? parsed.Sha ?? BuildsOf(pin).FirstOrDefault(b => b.Height == parsed.Height)?.Sha
            : null;

    private string Describe(PinView pin, string? version) {
        if (string.IsNullOrWhiteSpace(version)) return "none";
        if (version.Contains(',', StringComparison.Ordinal)) return $"mixed: {version}";
        return BuildVersion.Parse(version) is null ? version : BuildVersion.Describe(version, ShaOf(pin, version));
    }

    private string? CommitUrl(PinView pin, string? version) =>
        _repos.TryGetValue(pin.Upstream, out var repo) && ShaOf(pin, version) is { } sha ? $"{repo}/commit/{sha}" : null;

    private static string? Selected(PinView pin) => pin.Desired ?? pin.Committed;

    private string? Picked(PinView pin) => IsArmed(pin, ArmKind.Pick) ? _armed?.Version : Selected(pin);

    private bool FollowChecked(PinView pin) => pin.FollowLatest || IsArmed(pin, ArmKind.Follow);

    private string TakeLatestLabel(PinView pin) =>
        IsArmed(pin, ArmKind.TakeLatest) && BuildVersion.Parse(_armed?.Version) is { } parsed
            ? $"Confirm take latest #{parsed.Height}"
            : "Take latest";

    private static string ConfirmPinLabel(string? version) =>
        BuildVersion.Parse(version) is { } parsed ? $"Confirm pin #{parsed.Height}" : $"Confirm pin {version}";

    private bool RowWritable(PinView pin) => CanWrite && pin.Action != PinAction.NoToken;

    private bool IsSelfPin(PinView pin) => _client is { } client && Same(pin.App, client.AppName);

    private static (string Css, string Text) Badge(PinView pin) => pin.Action switch {
        PinAction.InSync => (MutedBadge, "In sync"),
        PinAction.Waiting => ("badge badge-accent2", "Pending"),
        PinAction.Commit => ("badge badge-accent", "Committing"),
        PinAction.NoToken => (MutedBadge, "No fleet token"),
        PinAction.NoTarget => (MutedBadge, "No target"),
        PinAction.NoPackages => ("badge badge-err", "No packages"),
        _ => (MutedBadge, "Not checked yet"),
    };

    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool IsFleetFailure(Exception e) =>
        e is HttpRequestException or TimeoutException or JsonException or OperationCanceledException;

    public void Dispose() {
        _disposed = true;
        _events?.Received -= OnEvent;
        _life.Cancel();
        _life.Dispose();
    }
}
