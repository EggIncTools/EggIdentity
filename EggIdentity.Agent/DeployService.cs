using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Resilience;

namespace EggIdentity.Agent;

public sealed class DeployService(
    AppCatalog catalog, IDockerEngine engine, IImageRegistry images, IStackWebhook webhook, DeployEventRing events,
    TimeProvider? time = null, Func<TimeSpan>? redeployTimeout = null) {
    private static readonly RetryOptions RegistryRetry = new() {
        MaxAttempts = 3,
        BaseDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(5),
    };

    private static readonly RetryOptions WebhookRetry = new() {
        MaxAttempts = 6,
        BaseDelay = TimeSpan.FromSeconds(5),
        MaxDelay = TimeSpan.FromSeconds(30),
        ShouldRetry = e => e is StackBusyException,
    };

    private static readonly TimeSpan RedeployPoll = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultRedeployTimeout = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _clock = time ?? TimeProvider.System;
    private readonly Func<TimeSpan> _redeployTimeout = redeployTimeout ?? (() => DefaultRedeployTimeout);
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AppState> _apps = catalog.Apps.ToDictionary(
        kv => kv.Key, kv => new AppState(kv.Value), StringComparer.OrdinalIgnoreCase);
    private AppCatalog _catalog = catalog;

    private sealed class AppState(DeployApp config) {
        public AppState(DeployApp config, AppState previous) : this(config) {
            Gate = previous.Gate;
            lock (previous.Sync) {
                AnnouncedDigest = previous.AnnouncedDigest;
                RunningDigest = previous.RunningDigest;
                RunningRevision = previous.RunningRevision;
                RunningVersion = previous.RunningVersion;
                LatestDigest = previous.LatestDigest;
                LatestRevision = previous.LatestRevision;
                LatestVersion = previous.LatestVersion;
                LastCheckedAt = previous.LastCheckedAt;
            }
        }

        public DeployApp Config { get; } = config;
        public ImageRef Image { get; } = ImageRef.Parse(config.Image);
        public DeployHandler Gate { get; } = new();
        public Lock Sync { get; } = new();
        public string? AnnouncedDigest { get; set; }
        public string? RunningDigest { get; set; }
        public string? RunningRevision { get; set; }
        public string? RunningVersion { get; set; }
        public string? LatestDigest { get; set; }
        public string? LatestRevision { get; set; }
        public string? LatestVersion { get; set; }
        public DateTimeOffset? LastCheckedAt { get; set; }
    }

    private sealed record Observation(ContainerInfo? Running, string? RunningDigest, string? LatestDigest, string? Error) {
        public bool UpdateAvailable => Running is not null && LatestDigest is not null && RunningDigest != LatestDigest;
    }

    public IReadOnlyCollection<string> AppNames {
        get {
            lock (_gate) {
                return [.. _apps.Keys];
            }
        }
    }

    public bool TryGetApp(string app, out DeployApp config) {
        if (TryGetState(app, out var state)) {
            config = state.Config;
            return true;
        }
        config = null!;
        return false;
    }

    public AppCatalogDiff Apply(AppCatalog next) {
        ArgumentNullException.ThrowIfNull(next);
        AppCatalogDiff diff;
        lock (_gate) {
            diff = _catalog.DiffTo(next);
            _catalog = next;
            foreach (var name in diff.Removed) _apps.Remove(name);
            foreach (var app in diff.Added.Concat(diff.Changed)) {
                try {
                    _apps[app.Name] = _apps.TryGetValue(app.Name, out var previous) ? new AppState(app, previous) : new AppState(app);
                } catch (Exception e) when (e is FormatException or ArgumentException) {
                    _apps.Remove(app.Name);
                    events.Publish(app.Name, DeployPhase.Failed, $"ignored: {e.Message}");
                }
            }
        }
        return diff;
    }

    public DeployStatus? Status(string app) => TryGetState(app, out var state) ? Snapshot(state) : null;

    public IReadOnlyList<DeployStatus> StatusAll() =>
        [.. States().OrderBy(s => s.Config.Name, StringComparer.OrdinalIgnoreCase).Select(Snapshot)];

    public void NoteRelease(string app, string digest, string? revision, string? version) {
        if (!TryGetState(app, out var state)) return;
        lock (state.Sync) {
            state.LatestDigest = digest;
            state.LatestRevision = revision;
            state.LatestVersion = version;
        }
    }

    public async Task<DeployStatus> CheckAsync(string app, CancellationToken ct) {
        var state = Require(app);
        var observation = await ObserveAsync(state, ct);
        var name = state.Config.Name;
        if (observation.Error is not null) {
            events.Publish(name, DeployPhase.Failed, $"check failed: {observation.Error}");
        } else if (observation.Running is null) {
            events.Publish(name, DeployPhase.Checked, $"container {state.Config.ContainerName} not found", digest: observation.LatestDigest);
        } else if (observation.UpdateAvailable && state.AnnouncedDigest != observation.LatestDigest) {
            state.AnnouncedDigest = observation.LatestDigest;
            events.Publish(name, DeployPhase.ReleaseAvailable, $"new image available for {state.Config.Image}",
                fromRevision: observation.Running.Revision, version: state.LatestVersion, digest: observation.LatestDigest);
        } else if (observation.UpdateAvailable) {
            events.Publish(name, DeployPhase.Checked, "update available, not yet deployed",
                fromRevision: observation.Running.Revision, digest: observation.LatestDigest);
        } else {
            events.Publish(name, DeployPhase.Checked, $"up to date at {Short(observation.Running.Revision)}",
                fromRevision: observation.Running.Revision, version: observation.Running.Version, digest: observation.RunningDigest);
        }
        return Snapshot(state);
    }

    public async Task<DeployStatus> DeployAsync(string app, string reason, CancellationToken ct) {
        var state = Require(app);
        if (!state.Gate.TryEnter()) return Snapshot(state);
        try {
            await DeployCoreAsync(state, reason, ct);
        } catch (Exception e) {
            events.Publish(state.Config.Name, DeployPhase.Failed, $"deploy failed: {e.Message}");
        } finally {
            state.Gate.Exit();
        }
        return Snapshot(state);
    }

    public async Task<DeployStatus> RestartAsync(string app, CancellationToken ct) {
        var state = Require(app);
        if (!state.Gate.TryEnter()) return Snapshot(state);
        var container = state.Config.ContainerName;
        try {
            events.Publish(state.Config.Name, DeployPhase.Restarting, $"restarting {container}");
            await engine.RestartAsync(container, ct);
            events.Publish(state.Config.Name, DeployPhase.Checked, $"restarted {container}",
                fromRevision: state.RunningRevision, toRevision: state.RunningRevision, version: state.RunningVersion, digest: state.RunningDigest);
        } catch (Exception e) {
            events.Publish(state.Config.Name, DeployPhase.Failed, $"restart failed: {e.Message}");
        } finally {
            state.Gate.Exit();
        }
        return Snapshot(state);
    }

    public async Task TickAsync(CancellationToken ct) {
        var states = States();
        var others = states.Where(s => !IsSelfApp(s)).ToList();
        var self = states.Where(IsSelfApp).ToList();

        await Task.WhenAll(others.Select(state => TickOneAsync(state, ct)));
        foreach (var state in self) await TickOneAsync(state, ct);
    }

    private bool IsSelfApp(AppState state) =>
        string.Equals(state.Config.ContainerName, SelfContainer.Name(), StringComparison.Ordinal);

    public async Task RunPollLoopAsync(Func<TimeSpan> interval, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(interval);
        using var timer = new PeriodicTimer(interval(), _clock);
        try {
            while (await timer.WaitForNextTickAsync(ct)) {
                await TickAsync(ct);
                var next = interval();
                if (next > TimeSpan.Zero && next != timer.Period) timer.Period = next;
            }
        } catch (OperationCanceledException) {
        }
    }

    private async Task TickOneAsync(AppState state, CancellationToken ct) {
        if (state.Gate.InProgress) return;
        DeployStatus status;
        try {
            status = await CheckAsync(state.Config.Name, ct);
        } catch (Exception e) {
            events.Publish(state.Config.Name, DeployPhase.Failed, $"check failed: {e.Message}");
            return;
        }
        if (state.Config.AutoDeploy && status.UpdateAvailable)
            await DeployAsync(state.Config.Name, "poll", ct);
    }

    private async Task DeployCoreAsync(AppState state, string reason, CancellationToken ct) {
        var cfg = state.Config;
        var container = cfg.ContainerName;
        var observation = await ObserveAsync(state, ct);
        if (observation.Error is not null) {
            events.Publish(cfg.Name, DeployPhase.Failed, $"deploy ({reason}) aborted: {observation.Error}");
            return;
        }
        if (observation.Running is null) {
            events.Publish(cfg.Name, DeployPhase.Failed, $"deploy ({reason}) aborted: container {container} not found");
            return;
        }
        if (string.IsNullOrWhiteSpace(cfg.WebhookUrl) || !Uri.TryCreate(cfg.WebhookUrl, UriKind.Absolute, out var url)) {
            events.Publish(cfg.Name, DeployPhase.Failed, $"deploy ({reason}) aborted: deploy.apps row {cfg.Name} has no usable webhook_url");
            return;
        }
        var old = observation.Running;
        if (!observation.UpdateAvailable) {
            events.Publish(cfg.Name, DeployPhase.UpToDate, $"already up to date at {Short(old.Revision)} ({reason})",
                fromRevision: old.Revision, toRevision: old.Revision, version: old.Version, digest: observation.RunningDigest);
            return;
        }
        state.AnnouncedDigest = observation.LatestDigest;

        events.Publish(cfg.Name, DeployPhase.Pulling, $"pulling {cfg.Image} ({reason})", fromRevision: old.Revision, digest: observation.LatestDigest);
        await engine.PullImageAsync(cfg.Image, new Progress<string>(line => Console.WriteLine($"pull {cfg.Name}: {line}")), ct);
        var pulled = await engine.InspectImageAsync(cfg.Image, ct)
            ?? throw new InvalidOperationException($"image {cfg.Image} missing after pull");
        var pulledDigest = PickDigest(pulled.RepoDigests, state.Image) ?? observation.LatestDigest;
        lock (state.Sync) {
            state.LatestRevision = pulled.Revision ?? state.LatestRevision;
            state.LatestVersion = pulled.Version ?? state.LatestVersion;
        }
        events.Publish(cfg.Name, DeployPhase.Pulled, $"pulled {Short(pulled.Revision)}",
            fromRevision: old.Revision, toRevision: pulled.Revision, version: pulled.Version, digest: pulledDigest);

        events.Publish(cfg.Name, DeployPhase.Recreating, $"asking Portainer to redeploy {container}",
            fromRevision: old.Revision, toRevision: pulled.Revision, version: pulled.Version, digest: pulledDigest);
        await Retry.RunAsync(token => webhook.InvokeAsync(url, token), WebhookRetry, _clock, ct);
        await WaitForRedeployAsync(cfg, pulled, ct);

        lock (state.Sync) {
            state.RunningDigest = pulledDigest;
            state.RunningRevision = pulled.Revision;
            state.RunningVersion = pulled.Version;
        }
        events.Publish(cfg.Name, DeployPhase.Deployed, $"deployed {Short(old.Revision)} -> {Short(pulled.Revision)}",
            fromRevision: old.Revision, toRevision: pulled.Revision, version: pulled.Version, digest: pulledDigest);
    }

    private async Task WaitForRedeployAsync(DeployApp cfg, ImageInfo pulled, CancellationToken ct) {
        var started = _clock.GetUtcNow();
        var timeout = _redeployTimeout();
        while (true) {
            ContainerInfo? running = null;
            try {
                running = await engine.InspectContainerAsync(cfg.ContainerName, ct);
            } catch (Exception e) when (e is not OperationCanceledException) {
                Console.WriteLine($"deploy {cfg.Name}: inspect while Portainer redeploys: {e.Message}");
            }
            if (running is { Running: true } && running.ImageId == pulled.Id) return;
            if (_clock.GetUtcNow() - started >= timeout)
                throw new InvalidOperationException($"{cfg.ContainerName} did not come up on {Short(pulled.Revision)} within {timeout}; check the stack in Portainer");
            await Task.Delay(RedeployPoll, _clock, ct);
        }
    }

    internal static string? ImageMismatch(ContainerInfo running, ImageRef expected) {
        if (running.Image.Length == 0 || running.Image.StartsWith("sha256:", StringComparison.Ordinal)) return null;
        ImageRef actual;
        try {
            actual = ImageRef.Parse(running.Image);
        } catch (FormatException) {
            return null;
        }
        return string.Equals(actual.Name, expected.Name, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"container {running.Name} runs {actual.Name}, but the deploy.apps row says {expected.Name}; fix the row before deploying";
    }

    private async Task<Observation> ObserveAsync(AppState state, CancellationToken ct) {
        ContainerInfo? running;
        string? latest;
        try {
            running = await engine.InspectContainerAsync(state.Config.ContainerName, ct);
            latest = await Retry.RunAsync(token => images.GetDigestAsync(state.Image, token), RegistryRetry, _clock, ct);
        } catch (Exception e) when (e is not OperationCanceledException) {
            lock (state.Sync) {
                state.LastCheckedAt = _clock.GetUtcNow();
            }
            return new Observation(null, null, null, e.Message);
        }

        if (running is not null && ImageMismatch(running, state.Image) is { } mismatch) {
            lock (state.Sync) {
                state.LastCheckedAt = _clock.GetUtcNow();
            }
            return new Observation(null, null, null, mismatch);
        }

        var runningDigest = running is null ? null : PickDigest(running, state.Image);
        lock (state.Sync) {
            state.LastCheckedAt = _clock.GetUtcNow();
            state.LatestDigest = latest;
            if (running is not null) {
                state.RunningDigest = runningDigest;
                state.RunningRevision = running.Revision;
                state.RunningVersion = running.Version;
            }
            if (state.LatestDigest == state.RunningDigest) {
                state.LatestRevision = state.RunningRevision;
                state.LatestVersion = state.RunningVersion;
            }
        }
        return new Observation(running, runningDigest, latest, null);
    }

    private DeployStatus Snapshot(AppState state) {
        lock (state.Sync) {
            var updateAvailable = state.LatestDigest is not null && state.RunningDigest is not null && state.LatestDigest != state.RunningDigest;
            return new DeployStatus(
                state.Config.Name,
                state.RunningDigest, state.RunningRevision, state.RunningVersion,
                state.LatestDigest, state.LatestRevision, state.LatestVersion,
                updateAvailable, state.LastCheckedAt, events.Latest(state.Config.Name), state.Gate.InProgress);
        }
    }

    private List<AppState> States() {
        lock (_gate) {
            return [.. _apps.Values];
        }
    }

    private bool TryGetState(string app, out AppState state) {
        lock (_gate) {
            return _apps.TryGetValue(app, out state!);
        }
    }

    private AppState Require(string app) =>
        TryGetState(app, out var state) ? state : throw new KeyNotFoundException($"unknown app \"{app}\"");

    internal static string? PickDigest(ContainerInfo container, ImageRef image) => PickDigest(container.RepoDigests, image);

    internal static string? PickDigest(IReadOnlyList<string> repoDigests, ImageRef image) {
        string? fallback = null;
        foreach (var entry in repoDigests) {
            var at = entry.IndexOf('@', StringComparison.Ordinal);
            if (at < 0) continue;
            var repo = entry[..at];
            var digest = entry[(at + 1)..];
            if (repo == image.Name || repo == $"{image.Registry}/{image.Repository}") return digest;
            fallback ??= digest;
        }
        return fallback;
    }

    private static string Short(string? revision) {
        if (string.IsNullOrEmpty(revision)) return "unknown";
        return revision.Length > 7 ? revision[..7] : revision;
    }
}
