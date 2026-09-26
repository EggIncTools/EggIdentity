using System.Collections.Concurrent;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Resilience;
using EggIdentity.Settings;

namespace EggIdentity.Fleet;

public sealed class DeployService(
    Func<CancellationToken, Task<IReadOnlyList<SuiteApp>>> apps,
    IPortainer? portainer,
    IImageRegistry images,
    DeployEventRing events,
    TimeProvider? time = null,
    Func<TimeSpan>? redeployTimeout = null) {
    public const string PortainerMissing = "portainer.api_url and portainer.api_key are not set on the identity host";

    private static readonly RetryOptions RegistryRetry = new() {
        MaxAttempts = 3,
        BaseDelay = TimeSpan.FromMilliseconds(500),
        MaxDelay = TimeSpan.FromSeconds(5),
    };

    private static readonly RetryOptions RedeployRetry = new() {
        MaxAttempts = 6,
        BaseDelay = TimeSpan.FromSeconds(5),
        MaxDelay = TimeSpan.FromSeconds(30),
        ShouldRetry = e => e is StackBusyException,
    };

    private static readonly TimeSpan RedeployPoll = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultRedeployTimeout = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _clock = time ?? TimeProvider.System;
    private readonly Func<TimeSpan> _redeployTimeout = redeployTimeout ?? (() => DefaultRedeployTimeout);
    private readonly ConcurrentDictionary<string, AppState> _states = new(StringComparer.OrdinalIgnoreCase);

    private sealed class AppState {
        public DeployHandler Gate { get; } = new();
        public Lock Sync { get; } = new();
        public string? AnnouncedDigest { get; set; }
        public string? Image { get; set; }
        public string? RunningDigest { get; set; }
        public string? RunningRevision { get; set; }
        public string? RunningVersion { get; set; }
        public bool? Running { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public string? LatestDigest { get; set; }
        public string? LatestRevision { get; set; }
        public string? LatestVersion { get; set; }
        public DateTimeOffset? LastCheckedAt { get; set; }
        public string? Problem { get; set; }
    }

    public sealed record Target(SuiteApp App, PortainerStack Stack, IDockerEngine Docker);

    private sealed record Resolution(Target? Target, string? Refusal);

    public DeployEventRing Events => events;

    public bool Configured => portainer is not null;

    public async Task<SuiteApp?> FindAppAsync(string name, CancellationToken ct) {
        var all = await apps(ct);
        return all.FirstOrDefault(a => a.Enabled && string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<SuiteApp>> AppsAsync(CancellationToken ct) =>
        [.. (await apps(ct)).Where(a => a.Enabled).OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)];

    public async Task<DeployStatus?> StatusAsync(string name, CancellationToken ct) {
        var app = await FindAppAsync(name, ct);
        return app is null ? null : Snapshot(app);
    }

    public async Task<IReadOnlyList<DeployStatus>> StatusAllAsync(CancellationToken ct) =>
        [.. (await AppsAsync(ct)).Select(Snapshot)];

    public void NoteRelease(string app, string digest, string? revision, string? version) {
        var state = State(app);
        lock (state.Sync) {
            state.LatestDigest = digest;
            state.LatestRevision = revision;
            state.LatestVersion = version;
        }
    }

    public async Task<ContainerInfo?> RunningAsync(SuiteApp app, CancellationToken ct) {
        var resolution = await ResolveAsync(app, ct);
        return resolution.Target is { } target ? await target.Docker.InspectContainerAsync(app.ContainerName, ct) : null;
    }

    public async Task<DeployStatus?> CheckAsync(string name, CancellationToken ct) {
        if (await FindAppAsync(name, ct) is not { } app) return null;
        var state = State(app.Name);
        var resolution = await ResolveAsync(app, ct);
        if (resolution.Target is not { } target) {
            Refuse(app, state, $"check failed: {resolution.Refusal}");
            return Snapshot(app);
        }

        var observation = await ObserveAsync(app, state, target, ct);
        if (observation.Error is not null) {
            events.Publish(app.Name, DeployPhase.Failed, $"check failed: {observation.Error}");
        } else if (observation.Running is null) {
            events.Publish(app.Name, DeployPhase.Checked, $"container {app.ContainerName} not found");
        } else if (observation.UpdateAvailable && state.AnnouncedDigest != observation.LatestDigest) {
            state.AnnouncedDigest = observation.LatestDigest;
            events.Publish(app.Name, DeployPhase.ReleaseAvailable, $"new image available for {observation.Running.Image}",
                fromRevision: observation.Running.Revision, version: state.LatestVersion, digest: observation.LatestDigest);
        } else if (observation.UpdateAvailable) {
            events.Publish(app.Name, DeployPhase.Checked, "update available, not yet deployed",
                fromRevision: observation.Running.Revision, digest: observation.LatestDigest);
        } else {
            events.Publish(app.Name, DeployPhase.Checked, $"up to date at {Short(observation.Running.Revision)}",
                fromRevision: observation.Running.Revision, version: observation.Running.Version, digest: observation.RunningDigest);
        }
        return Snapshot(app);
    }

    public async Task<DeployStatus?> DeployAsync(string name, string reason, CancellationToken ct) {
        if (await FindAppAsync(name, ct) is not { } app) return null;
        var state = State(app.Name);
        if (!state.Gate.TryEnter()) return Snapshot(app);
        try {
            await DeployCoreAsync(app, state, reason, ct);
        } catch (Exception e) {
            events.Publish(app.Name, DeployPhase.Failed, $"deploy failed: {e.Message}");
        } finally {
            state.Gate.Exit();
        }
        return Snapshot(app);
    }

    public async Task<string?> RestartAsync(string name, CancellationToken ct) {
        if (await FindAppAsync(name, ct) is not { } app) return $"unknown app \"{name}\"";
        var state = State(app.Name);
        if (!state.Gate.TryEnter()) return $"{app.Name} is busy";
        try {
            var resolution = await ResolveAsync(app, ct);
            if (resolution.Target is not { } target) {
                Refuse(app, state, $"restart refused: {resolution.Refusal}");
                return resolution.Refusal;
            }
            events.Publish(app.Name, DeployPhase.Restarting, $"restarting {app.ContainerName}");
            await target.Docker.RestartAsync(app.ContainerName, ct);
            events.Publish(app.Name, DeployPhase.Checked, $"restarted {app.ContainerName}",
                fromRevision: state.RunningRevision, toRevision: state.RunningRevision, version: state.RunningVersion, digest: state.RunningDigest);
            return null;
        } catch (Exception e) when (e is not OperationCanceledException) {
            events.Publish(app.Name, DeployPhase.Failed, $"restart failed: {e.Message}");
            return e.Message;
        } finally {
            state.Gate.Exit();
        }
    }

    public async Task<(string? Text, string? Refusal)> LogsAsync(string name, int lines, CancellationToken ct) {
        if (await FindAppAsync(name, ct) is not { } app) return (null, $"unknown app \"{name}\"");
        var resolution = await ResolveAsync(app, ct);
        return resolution.Target is not { } target
            ? (null, resolution.Refusal)
            : (await target.Docker.LogsTailAsync(app.ContainerName, Math.Clamp(lines, 1, 2000), ct), null);
    }

    public async Task<(IReadOnlyList<EnvKeyInfo>? Env, string? Refusal)> EnvAsync(string name, CancellationToken ct) {
        if (await FindAppAsync(name, ct) is not { } app) return (null, $"unknown app \"{name}\"");
        var resolution = await ResolveAsync(app, ct);
        if (resolution.Target is not { } target) return (null, resolution.Refusal);

        var container = await target.Docker.InspectContainerAsync(app.ContainerName, ct);
        if (container is null) return (null, $"container {app.ContainerName} not found");

        IReadOnlyList<string> imageEnv = [];
        var reference = container.ImageId.Length > 0 ? container.ImageId : container.Image;
        if (reference.Length > 0) imageEnv = (await target.Docker.InspectImageAsync(reference, ct))?.Env ?? [];

        ComposeServiceInfo? compose = null;
        try {
            compose = ComposeEnv.Parse(await portainer!.GetStackFileAsync(target.Stack, ct), app.ContainerName);
        } catch (Exception e) when (e is not OperationCanceledException) {
            Console.Error.WriteLine($"fleet: compose file for {app.Name} unavailable: {e.Message}");
        }
        return (EnvProvenance.Build(compose, container.Env, imageEnv, target.Stack.Env), null);
    }

    public async Task<string?> PatchEnvAsync(string name, IReadOnlyDictionary<string, string?> changes, CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0) return "no changes supplied";
        if (await FindAppAsync(name, ct) is not { } app) return $"unknown app \"{name}\"";
        var resolution = await ResolveAsync(app, ct);
        if (resolution.Target is not { } target) return resolution.Refusal;
        await portainer!.UpdateEnvAsync(target.Stack, changes, ct);
        return null;
    }

    public async Task<IReadOnlyList<StackInfo>> StacksAsync(CancellationToken ct) {
        var names = (await AppsAsync(ct))
            .Select(a => a.Stack?.Trim())
            .OfType<string>()
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (portainer is null) return [.. names.Select(n => StackLookup.Missing(n, PortainerMissing))];

        var stacks = await portainer.ListStacksAsync(ct);
        return [.. names.Select(n => StackLookup.Find(stacks, n) switch {
            ({ } stack, _) => StackLookup.Describe(stack),
            (_, var refusal) => StackLookup.Missing(n, refusal ?? "not found"),
        })];
    }

    public async Task<string?> RedeployStackAsync(string stackName, CancellationToken ct) {
        if (portainer is null) return PortainerMissing;
        var referenced = (await AppsAsync(ct)).Any(a => string.Equals(a.Stack?.Trim(), stackName, StringComparison.OrdinalIgnoreCase));
        if (!referenced) return $"no suite.apps row names stack \"{stackName}\"";
        var (stack, refusal) = StackLookup.Find(await portainer.ListStacksAsync(ct), stackName);
        if (stack is null) return refusal;
        if (StackLookup.Describe(stack).Refusal is { } notReady) return notReady;
        await Retry.RunAsync(token => portainer.RedeployAsync(stack, token), RedeployRetry, _clock, ct);
        return null;
    }

    public async Task TickAsync(bool autoDeploy, CancellationToken ct) {
        foreach (var app in await AppsAsync(ct)) {
            if (State(app.Name).Gate.InProgress) continue;
            try {
                var status = await CheckAsync(app.Name, ct);
                if (autoDeploy && app.AutoDeploy && status is { UpdateAvailable: true }) await DeployAsync(app.Name, "poll", ct);
            } catch (Exception e) when (e is not OperationCanceledException) {
                events.Publish(app.Name, DeployPhase.Failed, $"check failed: {e.Message}");
            }
        }
    }

    private async Task DeployCoreAsync(SuiteApp app, AppState state, string reason, CancellationToken ct) {
        var resolution = await ResolveAsync(app, ct);
        if (resolution.Target is not { } target) {
            Refuse(app, state, $"deploy ({reason}) aborted: {resolution.Refusal}");
            return;
        }
        if (StackLookup.Describe(target.Stack).Refusal is { } notReady) {
            Refuse(app, state, $"deploy ({reason}) aborted: {notReady}");
            return;
        }

        var observation = await ObserveAsync(app, state, target, ct);
        if (observation.Error is not null) {
            events.Publish(app.Name, DeployPhase.Failed, $"deploy ({reason}) aborted: {observation.Error}");
            return;
        }
        if (observation.Running is not { } old) {
            events.Publish(app.Name, DeployPhase.Failed, $"deploy ({reason}) aborted: container {app.ContainerName} not found");
            return;
        }
        if (!observation.UpdateAvailable) {
            events.Publish(app.Name, DeployPhase.UpToDate, $"already up to date at {Short(old.Revision)} ({reason})",
                fromRevision: old.Revision, toRevision: old.Revision, version: old.Version, digest: observation.RunningDigest);
            return;
        }
        state.AnnouncedDigest = observation.LatestDigest;

        var image = old.Image;
        events.Publish(app.Name, DeployPhase.Pulling, $"pulling {image} ({reason})", fromRevision: old.Revision, digest: observation.LatestDigest);
        await target.Docker.PullImageAsync(image, null, ct);
        var pulled = await target.Docker.InspectImageAsync(image, ct)
            ?? throw new InvalidOperationException($"image {image} missing after pull");
        var pulledDigest = PickDigest(pulled.RepoDigests, ImageRef.Parse(image)) ?? observation.LatestDigest;
        lock (state.Sync) {
            state.LatestRevision = pulled.Revision ?? state.LatestRevision;
            state.LatestVersion = pulled.Version ?? state.LatestVersion;
        }
        events.Publish(app.Name, DeployPhase.Pulled, $"pulled {Short(pulled.Revision)}",
            fromRevision: old.Revision, toRevision: pulled.Revision, version: pulled.Version, digest: pulledDigest);

        events.Publish(app.Name, DeployPhase.Recreating, $"asking Portainer to redeploy stack {target.Stack.Name}",
            fromRevision: old.Revision, toRevision: pulled.Revision, version: pulled.Version, digest: pulledDigest);
        await Retry.RunAsync(token => portainer!.RedeployAsync(target.Stack, token), RedeployRetry, _clock, ct);
        var running = await WaitForRedeployAsync(app, target, pulled, ct);

        lock (state.Sync) {
            state.RunningDigest = pulledDigest;
            state.RunningRevision = pulled.Revision;
            state.RunningVersion = pulled.Version;
            state.Running = true;
            state.StartedAt = running.StartedAt;
        }
        events.Publish(app.Name, DeployPhase.Deployed, $"deployed {Short(old.Revision)} -> {Short(pulled.Revision)}",
            fromRevision: old.Revision, toRevision: pulled.Revision, version: pulled.Version, digest: pulledDigest);
    }

    private async Task<ContainerInfo> WaitForRedeployAsync(SuiteApp app, Target target, ImageInfo pulled, CancellationToken ct) {
        var started = _clock.GetUtcNow();
        var timeout = _redeployTimeout();
        while (true) {
            ContainerInfo? running = null;
            try {
                running = await target.Docker.InspectContainerAsync(app.ContainerName, ct);
            } catch (Exception e) when (e is not OperationCanceledException) {
                Console.WriteLine($"fleet: {app.Name}: inspect while Portainer redeploys: {e.Message}");
            }
            if (running is { Running: true } && running.ImageId == pulled.Id) return running;
            if (_clock.GetUtcNow() - started >= timeout)
                throw new InvalidOperationException($"{app.ContainerName} did not come up on {Short(pulled.Revision)} within {timeout}; check the stack in Portainer");
            await Task.Delay(RedeployPoll, _clock, ct);
        }
    }

    private sealed record Observation(ContainerInfo? Running, string? RunningDigest, string? LatestDigest, string? Error) {
        public bool UpdateAvailable => Running is not null && LatestDigest is not null && RunningDigest != LatestDigest;
    }

    private async Task<Observation> ObserveAsync(SuiteApp app, AppState state, Target target, CancellationToken ct) {
        ContainerInfo? running;
        string? latest = null;
        try {
            running = await target.Docker.InspectContainerAsync(app.ContainerName, ct);
            if (running is not null && ImageRef.IsUntagged(running.Image)) {
                Stamp(state, running, null, null);
                return new Observation(null, null, null,
                    $"container {app.ContainerName} runs an untagged image ({running.Image}); give the service an image reference in the compose file");
            }
            if (running is not null)
                latest = await Retry.RunAsync(token => images.GetDigestAsync(ImageRef.Parse(running.Image), token), RegistryRetry, _clock, ct);
        } catch (Exception e) when (e is not OperationCanceledException) {
            lock (state.Sync) {
                state.LastCheckedAt = _clock.GetUtcNow();
            }
            return new Observation(null, null, null, e.Message);
        }

        var runningDigest = running is null ? null : PickDigest(running.RepoDigests, ImageRef.Parse(running.Image));
        Stamp(state, running, runningDigest, latest);
        return new Observation(running, runningDigest, latest, null);
    }

    private void Stamp(AppState state, ContainerInfo? running, string? runningDigest, string? latest) {
        lock (state.Sync) {
            state.LastCheckedAt = _clock.GetUtcNow();
            state.Problem = null;
            state.LatestDigest = latest ?? state.LatestDigest;
            state.Running = running?.Running;
            if (running is null) return;
            state.Image = running.Image;
            state.StartedAt = running.StartedAt;
            state.RunningDigest = runningDigest;
            state.RunningRevision = running.Revision;
            state.RunningVersion = running.Version;
            if (state.LatestDigest != state.RunningDigest) return;
            state.LatestRevision = state.RunningRevision;
            state.LatestVersion = state.RunningVersion;
        }
    }

    private async Task<Resolution> ResolveAsync(SuiteApp app, CancellationToken ct) {
        if (portainer is null) return new Resolution(null, PortainerMissing);
        if (string.IsNullOrWhiteSpace(app.Stack))
            return new Resolution(null, $"suite.apps row {app.Name} names no Portainer stack");
        var (stack, refusal) = StackLookup.Find(await portainer.ListStacksAsync(ct), app.Stack);
        return stack is null
            ? new Resolution(null, refusal)
            : new Resolution(new Target(app, stack, portainer.Docker(stack.EndpointId)), null);
    }

    private void Refuse(SuiteApp app, AppState state, string message) {
        lock (state.Sync) {
            state.Problem = message;
            state.LastCheckedAt = _clock.GetUtcNow();
        }
        events.Publish(app.Name, DeployPhase.Failed, message);
    }

    private AppState State(string app) => _states.GetOrAdd(app, _ => new AppState());

    private DeployStatus Snapshot(SuiteApp app) {
        var state = State(app.Name);
        lock (state.Sync) {
            var updateAvailable = state.LatestDigest is not null && state.RunningDigest is not null && state.LatestDigest != state.RunningDigest;
            return new DeployStatus(
                app.Name,
                state.RunningDigest, state.RunningRevision, state.RunningVersion,
                state.LatestDigest, state.LatestRevision, state.LatestVersion,
                updateAvailable, state.LastCheckedAt, events.Latest(app.Name), state.Gate.InProgress) {
                Image = state.Image,
                Stack = app.Stack,
                Running = state.Running,
                StartedAt = state.StartedAt,
                Problem = state.Problem,
            };
        }
    }

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

    private static string Short(string? revision) =>
        string.IsNullOrEmpty(revision) ? "unknown" : revision[..Math.Min(7, revision.Length)];
}
