using System.Text.Json;
using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Agent.Tests;

public class DeployServiceTests {
    private const string App = "eggledger";
    private const string Image = "ghcr.io/x/eggledger:latest";
    private const string OldDigest = "sha256:old";
    private const string NewDigest = "sha256:new";

    private static AppCatalog Catalog(bool autoDeploy = true) {
        var registry = new SettingsRegistry([], [DeployApps.Provider]);
        var row = new CollectionRow(DeployApps.Key, App, new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["name"] = App,
            ["image"] = Image,
            ["auto_deploy"] = autoDeploy ? "true" : "false",
        }, DateTimeOffset.UnixEpoch, null);
        var snapshot = new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null,
            new Dictionary<string, IReadOnlyList<CollectionRow>> { [DeployApps.Key] = [row] });
        return AppCatalog.FromSnapshot(snapshot);
    }

    private static (DeployService Service, FakeEngine Engine, FakeRegistry Images, DeployEventRing Ring) Build(
        string? latestDigest = NewDigest, bool autoDeploy = true, Func<ContainerInfo, bool>? isSelf = null) {
        var engine = new FakeEngine();
        var images = new FakeRegistry(latestDigest);
        var ring = new DeployEventRing();
        var service = new DeployService(Catalog(autoDeploy), engine, images, ring, new ZeroDelayTimeProvider(), isSelf ?? (_ => false));
        return (service, engine, images, ring);
    }

    private sealed class ZeroDelayTimeProvider : TimeProvider {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
            if (dueTime != Timeout.InfiniteTimeSpan) ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new NoopTimer();
        }

        private sealed class NoopTimer : ITimer {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private static IEnumerable<DeployPhase> Phases(DeployEventRing ring) => ring.Since(0).Select(e => e.Phase);

    private static JsonElement Json(string text) {
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task Check_UpToDate_PublishesChecked() {
        var (service, _, _, ring) = Build(latestDigest: OldDigest);

        var status = await service.CheckAsync(App, CancellationToken.None);

        Assert.False(status.UpdateAvailable);
        Assert.Equal(OldDigest, status.RunningDigest);
        Assert.Equal("rev-old", status.RunningRevision);
        Assert.Equal([DeployPhase.Checked], Phases(ring));
    }

    [Fact]
    public async Task Check_NewDigest_PublishesReleaseAvailableOnce() {
        var (service, _, _, ring) = Build();

        var first = await service.CheckAsync(App, CancellationToken.None);
        var second = await service.CheckAsync(App, CancellationToken.None);

        Assert.True(first.UpdateAvailable);
        Assert.True(second.UpdateAvailable);
        Assert.Equal(NewDigest, second.LatestDigest);
        Assert.Equal([DeployPhase.ReleaseAvailable, DeployPhase.Checked], Phases(ring));
    }

    [Fact]
    public async Task Check_RegistryFailure_PublishesFailedAndDoesNotThrow() {
        var (service, _, images, ring) = Build();
        images.Fail = new HttpRequestException("registry down");

        var status = await service.CheckAsync(App, CancellationToken.None);

        Assert.False(status.UpdateAvailable);
        Assert.Equal([DeployPhase.Failed], Phases(ring));
        Assert.Contains("registry down", ring.Latest(App)!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_UpToDate_PublishesUpToDateAndTouchesNothing() {
        var (service, engine, _, ring) = Build(latestDigest: OldDigest);

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.UpToDate], Phases(ring));
        Assert.Equal(["inspect"], engine.Calls);
    }

    [Fact]
    public async Task Deploy_NewImage_RunsFullPhaseSequenceAndSwapsContainers() {
        var (service, engine, _, ring) = Build();

        var status = await service.DeployAsync(App, "hook", CancellationToken.None);

        Assert.Equal([DeployPhase.Pulling, DeployPhase.Pulled, DeployPhase.Recreating, DeployPhase.Deployed], Phases(ring));
        Assert.Equal(
            ["inspect", "pull", "inspect-image", "rename old-id -> eggledger-old", "create eggledger", "start new-id", "stop old-id", "remove old-id"],
            engine.Calls);
        Assert.Equal(NewDigest, status.RunningDigest);
        Assert.Equal("rev-new", status.RunningRevision);
        Assert.Equal("v2", status.RunningVersion);
        Assert.False(status.UpdateAvailable);
        Assert.False(status.Busy);

        var deployed = ring.Latest(App)!;
        Assert.Equal("rev-old", deployed.FromRevision);
        Assert.Equal("rev-new", deployed.ToRevision);
        Assert.Equal("v2", deployed.Version);
        Assert.Equal(NewDigest, deployed.Digest);
        Assert.Equal(Image, engine.CreatedImage);
    }

    [Fact]
    public async Task Deploy_StartFails_RollsBackAndPublishesFailed() {
        var (service, engine, _, ring) = Build();
        engine.StartFailure = new InvalidOperationException("port already allocated");

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.Pulling, DeployPhase.Pulled, DeployPhase.Recreating, DeployPhase.Failed], Phases(ring));
        Assert.Contains("port already allocated", ring.Latest(App)!.Message, StringComparison.Ordinal);
        Assert.Contains("start new-id", engine.Calls);
        Assert.Contains("stop old-id", engine.Calls);
        Assert.Contains("stop new-id", engine.Calls);
        Assert.Contains("remove new-id", engine.Calls);
        Assert.Contains("rename old-id -> eggledger", engine.Calls);
        Assert.Contains("start old-id", engine.Calls);
        Assert.DoesNotContain("remove old-id", engine.Calls);
        Assert.True(engine.Calls.IndexOf("rename old-id -> eggledger") < engine.Calls.IndexOf("start old-id"));
    }

    [Fact]
    public async Task Deploy_RollbackRenameFails_OriginalCauseStillSurfaces() {
        var (service, engine, _, ring) = Build();
        engine.StartFailure = new InvalidOperationException("port already allocated");
        engine.RenameFailures["eggledger"] = new InvalidOperationException("rename boom");

        await service.DeployAsync(App, "manual", CancellationToken.None);

        var failed = ring.Latest(App)!;
        Assert.Equal(DeployPhase.Failed, failed.Phase);
        Assert.Contains("port already allocated", failed.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("rename boom", failed.Message, StringComparison.Ordinal);
        Assert.Contains("rename old-id -> eggledger", engine.Calls);
        Assert.Contains("start old-id", engine.Calls);
    }

    [Fact]
    public async Task Deploy_SelfContainer_HandsOffToSwapHelperWithoutStoppingItself() {
        var (service, engine, _, ring) = Build(isSelf: c => c.Id == "old-id");
        engine.Container = engine.Container! with {
            Env = ["DOCKER_HOST=unix:///run/docker.sock", "OTHER=1"],
            HostConfig = Json("""{ "Binds": ["/data:/data", "/run/docker.sock:/var/run/docker.sock:ro"], "PortBindings": { "80/tcp": [] } }"""),
        };

        var status = await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.Pulling, DeployPhase.Pulled, DeployPhase.Recreating], Phases(ring));
        Assert.Equal("handing off to swap helper", ring.Latest(App)!.Message);
        Assert.Equal(
            ["inspect", "pull", "inspect-image", "rename old-id -> eggledger-old", "create eggledger", "create eggledger-swap", "start swap-id"],
            engine.Calls);
        Assert.False(status.Busy);

        var helper = engine.Specs.Single(s => s.Name == "eggledger-swap");
        Assert.Equal(Image, helper.Image);
        Assert.Equal(["swap", "old-id", "new-id", "eggledger"], helper.Cmd);
        Assert.Equal(["/run/docker.sock:/var/run/docker.sock:ro"], helper.Binds);
        Assert.True(helper.AutoRemove);
        Assert.Equal("none", helper.NetworkMode);
        Assert.Equal(["DOCKER_HOST=unix:///run/docker.sock"], helper.Config.GetProperty("Env").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(JsonValueKind.Object, helper.HostConfig.ValueKind);
        Assert.False(helper.HostConfig.TryGetProperty("PortBindings", out _));

        var replacement = engine.Specs.Single(s => s.Name == "eggledger");
        Assert.Null(replacement.Cmd);
        Assert.Null(replacement.Binds);
        Assert.False(replacement.AutoRemove);
    }

    [Fact]
    public async Task Deploy_SelfContainer_HelperStartFails_RollsBackAndPublishesFailed() {
        var (service, engine, _, ring) = Build(isSelf: c => c.Id == "old-id");
        engine.StartFailure = new InvalidOperationException("helper refused");

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal(DeployPhase.Failed, ring.Latest(App)!.Phase);
        Assert.Contains("helper refused", ring.Latest(App)!.Message, StringComparison.Ordinal);
        Assert.Contains("remove new-id", engine.Calls);
        Assert.Contains("rename old-id -> eggledger", engine.Calls);
        Assert.DoesNotContain("stop old-id", engine.Calls);
        Assert.DoesNotContain("start old-id", engine.Calls);
    }

    [Fact]
    public async Task Apply_ChangedRow_KeepsInFlightGate() {
        var (service, engine, _, ring) = Build();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.PullGate = release.Task;

        var first = service.DeployAsync(App, "manual", CancellationToken.None);
        await engine.PullStarted.Task;
        var diff = service.Apply(new AppCatalog([new DeployApp { Name = App, Image = "ghcr.io/x/eggledger:v2", AutoDeploy = false }]));
        var second = await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([App], diff.Changed.Select(a => a.Name));
        Assert.True(second.Busy);
        Assert.Equal(1, engine.Calls.Count(c => c == "pull"));
        release.SetResult();
        await first;

        Assert.False(service.Status(App)!.Busy);
        Assert.Equal(DeployPhase.Deployed, ring.Latest(App)!.Phase);
    }

    [Fact]
    public async Task Deploy_StartFailsAlongsideOld_SucceedsAfterStoppingOld() {
        var (service, engine, _, ring) = Build();
        engine.StartFailuresRemaining = 1;

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal(DeployPhase.Deployed, ring.Latest(App)!.Phase);
        Assert.Equal(2, engine.Calls.Count(c => c == "start new-id"));
        Assert.Equal(1, engine.Calls.Count(c => c == "stop old-id"));
        Assert.Contains("remove old-id", engine.Calls);
    }

    [Fact]
    public async Task Deploy_ContainerMissing_PublishesFailed() {
        var (service, engine, _, ring) = Build();
        engine.Container = null;

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.Failed], Phases(ring));
        Assert.DoesNotContain("pull", engine.Calls);
    }

    [Fact]
    public async Task Deploy_WhileBusy_ReturnsBusyStatusWithoutSecondRun() {
        var (service, engine, _, ring) = Build();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.PullGate = release.Task;

        var first = service.DeployAsync(App, "manual", CancellationToken.None);
        await engine.PullStarted.Task;
        var second = await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.True(second.Busy);
        release.SetResult();
        await first;

        Assert.Equal(1, engine.Calls.Count(c => c == "pull"));
        Assert.Equal(DeployPhase.Deployed, ring.Latest(App)!.Phase);
    }

    [Fact]
    public async Task Tick_AutoDeployOn_ChecksThenDeploys() {
        var (service, engine, _, ring) = Build();

        await service.TickAsync(CancellationToken.None);

        Assert.Equal(DeployPhase.ReleaseAvailable, ring.Since(0)[0].Phase);
        Assert.Equal(DeployPhase.Deployed, ring.Latest(App)!.Phase);
        Assert.Contains("pull", engine.Calls);
    }

    [Fact]
    public async Task Tick_AutoDeployOff_OnlyAnnounces() {
        var (service, engine, _, ring) = Build(autoDeploy: false);

        await service.TickAsync(CancellationToken.None);

        Assert.Equal([DeployPhase.ReleaseAvailable], Phases(ring));
        Assert.DoesNotContain("pull", engine.Calls);
    }

    [Fact]
    public async Task Restart_PublishesRestartingThenCompletion() {
        var (service, engine, _, ring) = Build();

        await service.RestartAsync(App, CancellationToken.None);

        Assert.Equal([DeployPhase.Restarting, DeployPhase.Checked], Phases(ring));
        Assert.Contains("restart eggledger", engine.Calls);
    }

    [Fact]
    public async Task Reap_LeftoverOldContainer_RemovedWhenCurrentIsRunning() {
        var (service, engine, _, ring) = Build();
        engine.OldLeftover = true;

        await service.ReapAsync(CancellationToken.None);

        Assert.Contains("remove old-id", engine.Calls);
        Assert.Equal([DeployPhase.Deployed], Phases(ring));
    }

    [Fact]
    public void NoteRelease_SurfacesRevisionAndVersionInStatus() {
        var (service, _, _, _) = Build();

        service.NoteRelease(App, NewDigest, "rev-hook", "v9");

        var status = service.Status(App)!;
        Assert.Equal(NewDigest, status.LatestDigest);
        Assert.Equal("rev-hook", status.LatestRevision);
        Assert.Equal("v9", status.LatestVersion);
    }

    [Fact]
    public async Task Apply_AddsRemovesAndReplacesAppsAtRuntime() {
        var (service, engine, _, ring) = Build();

        var diff = service.Apply(new AppCatalog([
            new DeployApp { Name = App, Image = "ghcr.io/x/eggledger:v2", AutoDeploy = false },
            new DeployApp { Name = "other", Image = "ghcr.io/x/other:latest", AutoDeploy = false },
        ]));

        Assert.Equal(["other"], diff.Added.Select(a => a.Name));
        Assert.Equal([App], diff.Changed.Select(a => a.Name));
        Assert.Empty(diff.Removed);
        Assert.True(service.TryGetApp("OTHER", out _));
        Assert.True(service.TryGetApp(App, out var replaced));
        Assert.Equal("ghcr.io/x/eggledger:v2", replaced.Image);

        await service.TickAsync(CancellationToken.None);
        Assert.DoesNotContain("pull", engine.Calls);
        Assert.Contains(ring.Since(0), e => e.App == "other");

        var removal = service.Apply(new AppCatalog([]));
        Assert.Equal(2, removal.Removed.Count);
        Assert.Empty(service.AppNames);
        Assert.Null(service.Status(App));
    }

    [Fact]
    public void Apply_BadImageReference_IsDroppedWithFailedEvent() {
        var (service, _, _, ring) = Build();

        service.Apply(new AppCatalog([new DeployApp { Name = "broken", Image = "bad:" }]));

        Assert.False(service.TryGetApp("broken", out _));
        Assert.Equal(DeployPhase.Failed, ring.Latest("broken")!.Phase);
    }

    [Fact]
    public void PickDigest_PrefersMatchingRepository() {
        var image = ImageRef.Parse(Image);
        var digest = DeployService.PickDigest(["ghcr.io/other/thing@sha256:a", "ghcr.io/x/eggledger@sha256:b"], image);

        Assert.Equal("sha256:b", digest);
    }

    [Fact]
    public void PickDigest_NoMatch_FallsBackToFirst() {
        var image = ImageRef.Parse(Image);
        Assert.Equal("sha256:a", DeployService.PickDigest(["ghcr.io/other/thing@sha256:a"], image));
        Assert.Null(DeployService.PickDigest([], image));
    }

    private sealed class FakeRegistry(string? digest) : IImageRegistry {
        public Exception? Fail { get; set; }

        public Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) {
            if (Fail is not null) throw Fail;
            return Task.FromResult(digest ?? throw new InvalidOperationException("no digest"));
        }
    }

    private sealed class FakeEngine : IDockerEngine {
        private static readonly JsonElement Empty = EmptyObject();

        public List<string> Calls { get; } = [];
        public ContainerInfo? Container { get; set; } = MakeContainer("old-id", "eggledger", OldDigest, "rev-old", "v1", running: true);
        public bool OldLeftover { get; set; }
        public Exception? StartFailure { get; set; }
        public int StartFailuresRemaining { get; set; }
        public Dictionary<string, Exception> RenameFailures { get; } = new(StringComparer.Ordinal);
        public Task? PullGate { get; set; }
        public TaskCompletionSource PullStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<ContainerSpec> Specs { get; } = [];
        public string? CreatedImage { get; private set; }
        private bool _pulled;

        public Task<ContainerInfo?> InspectContainerAsync(string name, CancellationToken ct) {
            if (name.EndsWith("-old", StringComparison.Ordinal)) {
                Calls.Add("inspect-old");
                return Task.FromResult(OldLeftover ? MakeContainer("old-id", name, OldDigest, "rev-old", "v1", running: false) : null);
            }
            Calls.Add("inspect");
            return Task.FromResult(Container);
        }

        public Task<ImageInfo?> InspectImageAsync(string reference, CancellationToken ct) {
            Calls.Add("inspect-image");
            var digest = _pulled ? NewDigest : OldDigest;
            var revision = _pulled ? "rev-new" : "rev-old";
            var version = _pulled ? "v2" : "v1";
            return Task.FromResult<ImageInfo?>(new ImageInfo("sha256:img", [$"ghcr.io/x/eggledger@{digest}"], Labels(revision, version), []));
        }

        public async Task PullImageAsync(string reference, IProgress<string>? progress, CancellationToken ct) {
            Calls.Add("pull");
            PullStarted.TrySetResult();
            if (PullGate is not null) await PullGate;
            progress?.Report("Downloading");
            _pulled = true;
        }

        public Task RenameAsync(string name, string newName, CancellationToken ct) {
            Calls.Add($"rename {name} -> {newName}");
            if (RenameFailures.TryGetValue(newName, out var failure)) throw failure;
            return Task.CompletedTask;
        }

        public Task<string> CreateAsync(ContainerSpec spec, CancellationToken ct) {
            Calls.Add($"create {spec.Name}");
            Specs.Add(spec);
            if (spec.Name.EndsWith("-swap", StringComparison.Ordinal)) return Task.FromResult("swap-id");
            CreatedImage = spec.Image;
            return Task.FromResult("new-id");
        }

        public Task StartAsync(string name, CancellationToken ct) {
            Calls.Add($"start {name}");
            if (name is "new-id" or "swap-id" && StartFailure is not null) throw StartFailure;
            if (name == "new-id" && StartFailuresRemaining > 0) {
                StartFailuresRemaining--;
                throw new InvalidOperationException("address already in use");
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(string name, CancellationToken ct) {
            Calls.Add($"stop {name}");
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string name, CancellationToken ct) {
            Calls.Add($"remove {name}");
            return Task.CompletedTask;
        }

        public Task RestartAsync(string name, CancellationToken ct) {
            Calls.Add($"restart {name}");
            return Task.CompletedTask;
        }

        public Task<string> LogsTailAsync(string name, int lines, CancellationToken ct) {
            Calls.Add($"logs {name}");
            return Task.FromResult("log");
        }

        private static JsonElement EmptyObject() {
            using var doc = JsonDocument.Parse("{}");
            return doc.RootElement.Clone();
        }

        private static ContainerInfo MakeContainer(string id, string name, string digest, string revision, string version, bool running) =>
            new(id, name, Image, "sha256:img", [$"ghcr.io/x/eggledger@{digest}"], [], Labels(revision, version), running, Empty, Empty, Empty);

        private static Dictionary<string, string> Labels(string revision, string version) => new(StringComparer.Ordinal) {
            [OciLabels.Revision] = revision,
            [OciLabels.Version] = version,
        };
    }
}
