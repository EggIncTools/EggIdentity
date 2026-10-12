using EggIdentity.Contract;
using EggIdentity.Deploy;
using Microsoft.Extensions.Logging;

namespace EggIdentity.Fleet;

public sealed class PinReconciler(
    Func<CancellationToken, Task<(IReadOnlyList<SuiteApp> Apps, IReadOnlyList<SuiteUpstream> Upstreams, IReadOnlyList<AppPin> Pins)>> config,
    IGitHubRepo github,
    IUpstreamBuilds builds,
    IPinStates states,
    Func<bool> hasToken,
    DeployEventRing events) : IDisposable {
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<PinView>? _views;

    public bool HasToken => hasToken();

    public async Task<IReadOnlyList<PinView>> ReconcileAsync(string? app, CancellationToken ct) {
        await _gate.WaitAsync(ct);
        try {
            var views = await PassAsync(app, ct);
            var previous = Volatile.Read(ref _views);
            if (app is null) Volatile.Write(ref _views, views);
            else if (previous is not null) Volatile.Write(ref _views, [.. previous.Where(v => !Same(v.App, app)), .. views]);
            return views;
        } finally {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<PinView>> ViewsAsync(CancellationToken ct) {
        if (Volatile.Read(ref _views) is { } views) return views;
        var (_, upstreams, pins) = await config(ct);
        var known = await StatesAsync(ct);
        var token = hasToken();
        var latest = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PinView>();
        foreach (var pin in pins) {
            var upstream = upstreams.FirstOrDefault(u => Same(u.Name, pin.Upstream));
            var newest = upstream is null ? null : await LatestAsync(upstream, latest, ct);
            var state = known.GetValueOrDefault(SuiteUpstreams.PinId(pin.App, pin.Upstream));
            var desired = Desired(pin);
            var target = PinDecision.Target(new PinInputs(desired, pin.FollowLatest, newest, [], state?.PendingVersion, token));
            var action = (token, target) switch {
                (false, _) => PinAction.NoToken,
                (_, null) => PinAction.NoTarget,
                _ => PinAction.Unknown,
            };
            result.Add(new PinView(pin.App, pin.Upstream, pin.Branch, null, desired, pin.FollowLatest, newest, state?.PendingVersion, state?.LastError, action));
        }
        return result;
    }

    public async Task NoteHookAsync(string app, string revision, CancellationToken ct) {
        ArgumentException.ThrowIfNullOrWhiteSpace(app);
        if (string.IsNullOrWhiteSpace(revision)) return;
        await _gate.WaitAsync(ct);
        try {
            var pending = (await StatesAsync(ct)).Values
                .Where(s => Same(s.App, app))
                .Select(s => (State: s, Commit: s.PendingCommit))
                .Where(p => !string.IsNullOrWhiteSpace(p.Commit))
                .ToList();
            if (pending.Count == 0) return;
            var (apps, _, _) = await config(ct);
            if (RepoRef.Parse(apps.FirstOrDefault(a => Same(a.Name, app))?.RepoUrl) is not { } repo) return;
            foreach (var (state, commit) in pending) {
                if (commit is null || !await LandedAsync(repo, commit, revision.Trim(), ct)) continue;
                await states.ClearPendingAsync(state.App, state.Upstream, ct);
                if (Volatile.Read(ref _views) is { } views) Volatile.Write(ref _views, Cleared(views, state.App, state.Upstream));
                events.Publish(state.App, DeployPhase.Checked, $"pin {state.Upstream} {BuildVersion.Describe(state.PendingVersion, null)} reached a deployed build");
            }
        } finally {
            _gate.Release();
        }
    }

    private IReadOnlyList<PinView> Cleared(IReadOnlyList<PinView> views, string app, string upstream) {
        var token = hasToken();
        return [.. views.Select(v => Same(v.App, app) && Same(v.Upstream, upstream) ? Recomputed(v, token) : v)];
    }

    private static PinView Recomputed(PinView view, bool token) {
        IReadOnlyList<string> committed = string.IsNullOrWhiteSpace(view.Committed)
            ? []
            : view.Committed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var inputs = new PinInputs(view.Desired, view.FollowLatest, view.Latest, committed, null, token);
        return view with { PendingVersion = null, Action = PinDecision.Decide(inputs) };
    }

    public void Dispose() => _gate.Dispose();

    private async Task<Dictionary<string, PinState>> StatesAsync(CancellationToken ct) =>
        (await states.GetAllAsync(ct))
            .GroupBy(s => SuiteUpstreams.PinId(s.App, s.Upstream), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.UpdatedAt).First(), StringComparer.OrdinalIgnoreCase);

    private async Task<string?> LatestAsync(SuiteUpstream upstream, Dictionary<string, string?> cache, CancellationToken ct) {
        if (!cache.TryGetValue(upstream.Name, out var version))
            cache[upstream.Name] = version = (await builds.LatestAsync(upstream.Name, ct))?.Version;
        return version;
    }

    private async Task<bool> LandedAsync(RepoRef repo, string commit, string revision, CancellationToken ct) {
        if (string.Equals(commit, revision, StringComparison.OrdinalIgnoreCase)) return true;
        if (!hasToken()) return false;
        try {
            return await github.IsAncestorAsync(repo, commit, revision, ct);
        } catch (Exception e) when (!ct.IsCancellationRequested) {
            events.Logger?.LogWarning(e, "pin ancestry check failed for {Repo}", $"{repo.Owner}/{repo.Name}");
            return false;
        }
    }

    private async Task<IReadOnlyList<PinView>> PassAsync(string? app, CancellationToken ct) {
        var (apps, upstreams, pins) = await config(ct);
        var known = await StatesAsync(ct);
        var token = hasToken();
        var latest = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var views = new List<PinView>();
        var selected = pins.Where(p => app is null || Same(p.App, app));
        foreach (var group in selected.GroupBy(p => (App: p.App.Trim().ToLowerInvariant(), p.Branch, p.PropsPath))) {
            var pass = new FilePass(group.First(), apps, known);
            if (token) await pass.ReadAsync(github, ct);
            foreach (var pin in group) {
                var upstream = upstreams.FirstOrDefault(u => Same(u.Name, pin.Upstream));
                var newest = upstream is null ? null : await LatestAsync(upstream, latest, ct);
                views.Add(await PinAsync(pass, pin, upstream, newest, token, ct));
            }
        }
        return views;
    }

    private async Task<PinView> PinAsync(FilePass pass, AppPin pin, SuiteUpstream? upstream, string? latest, bool token, CancellationToken ct) {
        var state = pass.State(pin);
        var desired = Desired(pin);
        var committed = token && upstream is not null && pass.File is { } file ? PinRewrite.Read(file.Text, upstream.PackagePrefix) : [];
        var inputs = new PinInputs(desired, pin.FollowLatest, latest, committed, state?.PendingVersion, token);
        var view = new PinView(
            pin.App, pin.Upstream, pin.Branch, committed.Count == 0 ? null : string.Join(", ", committed),
            desired, pin.FollowLatest, latest, state?.PendingVersion, state?.LastError, PinDecision.Decide(inputs));
        if (!token) return view;

        var target = PinDecision.Target(inputs);
        if (Problem(pass, pin, upstream, target, view.Action) is { } problem) return await FailAsync(pin, state, view, problem, ct);
        if (view.Action == PinAction.Commit && target is not null && upstream is not null)
            return await CommitAsync(pass, pin, upstream, state, view, target, ct);
        if (view.Action is PinAction.InSync or PinAction.NoTarget && state is { LastError: not null } or { PendingCommit: not null }) {
            await states.ClearPendingAsync(pin.App, pin.Upstream, ct);
            return view with { PendingVersion = null, Error = null };
        }
        return view;
    }

    private static string? Problem(FilePass pass, AppPin pin, SuiteUpstream? upstream, string? target, PinAction action) {
        if (upstream is null) return $"no upstream named \"{pin.Upstream}\"";
        if (pass.Failure is { } failure) return failure;
        if (target is not null && !IsBuildVersion(target)) return $"invalid version \"{target}\"";
        return action == PinAction.NoPackages ? $"no {upstream.PackagePrefix} packages in {pin.PropsPath} on {pin.Branch}" : null;
    }

    private async Task<PinView> CommitAsync(
        FilePass pass, AppPin pin, SuiteUpstream upstream, PinState? state, PinView view, string target, CancellationToken ct) {
        if (pass.File is not { } file || pass.Repo is not { } repo) return view;
        var text = PinRewrite.Apply(file.Text, upstream.PackagePrefix, target);
        try {
            var commit = await github.PutFileAsync(repo, pin.PropsPath, pin.Branch, text, file.Sha, $"pin {pin.Upstream} {target}", ct);
            pass.File = new RepoFile(text, commit.BlobSha);
            await states.SetPendingAsync(pin.App, pin.Upstream, target, commit.CommitSha, ct);
            events.Publish(pin.App, DeployPhase.Checked, $"pin {pin.Upstream} {BuildVersion.Describe(target, null)} committed to {pin.Branch}", toRevision: commit.CommitSha, version: target);
            return view with { PendingVersion = target, Error = null };
        } catch (Exception e) when (!ct.IsCancellationRequested) {
            return await FailAsync(pin, state, view, Describe(e), ct);
        }
    }

    private async Task<PinView> FailAsync(AppPin pin, PinState? state, PinView view, string error, CancellationToken ct) {
        if (state is null || state.LastError != error || state.PendingCommit is not null) {
            await states.SetErrorAsync(pin.App, pin.Upstream, error, ct);
            events.Publish(pin.App, DeployPhase.Checked, $"pin {pin.Upstream} failed: {error}");
        }
        return view with { PendingVersion = null, Error = error };
    }

    private static string? Desired(AppPin pin) => string.IsNullOrWhiteSpace(pin.DesiredVersion) ? null : pin.DesiredVersion.Trim();

    private static string Describe(Exception e) => e is GitHubRejectedException ? e.Message : $"{e.GetType().Name}: {e.Message}";

    private static bool IsBuildVersion(string value) => BuildVersion.Parse(value) is { Sha: null };

    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed class FilePass(AppPin first, IReadOnlyList<SuiteApp> apps, Dictionary<string, PinState> known) {
        public RepoRef? Repo { get; private set; }

        public RepoFile? File { get; set; }

        public string? Failure { get; private set; }

        public PinState? State(AppPin pin) => known.GetValueOrDefault(SuiteUpstreams.PinId(pin.App, pin.Upstream));

        public async Task ReadAsync(IGitHubRepo github, CancellationToken ct) {
            var app = apps.FirstOrDefault(a => Same(a.Name, first.App));
            if (app is null) {
                Failure = $"no suite app named \"{first.App}\"";
                return;
            }
            Repo = RepoRef.Parse(app.RepoUrl);
            if (Repo is null) {
                Failure = $"\"{first.App}\" has no GitHub repository URL";
                return;
            }
            if (string.IsNullOrWhiteSpace(first.Branch)) {
                Failure = "no branch set";
                return;
            }
            try {
                File = await github.GetFileAsync(Repo, first.PropsPath, first.Branch, ct);
                if (File is null) Failure = $"props file {first.PropsPath} not found on {first.Branch}";
            } catch (Exception e) when (!ct.IsCancellationRequested) {
                Failure = Describe(e);
            }
        }
    }
}
