using EggIdentity.Contract;
using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Agent.Tests;

public class DeployServiceTests {
    private const string App = "eggledger";
    private const string Image = "ghcr.io/x/eggledger:latest";
    private const string OldDigest = "sha256:old";
    private const string NewDigest = "sha256:new";
    private const string OldImageId = "sha256:img";
    private const string NewImageId = "sha256:img-new";
    private const string WebhookUrl = "https://portainer.test/api/stacks/webhooks/abc";

    private static AppCatalog Catalog(bool autoDeploy = true, string? webhookUrl = WebhookUrl) {
        var registry = new SettingsRegistry([], [DeployApps.Provider]);
        var row = new CollectionRow(DeployApps.Key, App, new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["name"] = App,
            ["image"] = Image,
            ["auto_deploy"] = autoDeploy ? "true" : "false",
            ["webhook_url"] = webhookUrl,
        }, DateTimeOffset.UnixEpoch, null);
        var snapshot = new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null,
            new Dictionary<string, IReadOnlyList<CollectionRow>> { [DeployApps.Key] = [row] });
        return AppCatalog.FromSnapshot(snapshot);
    }

    private static (DeployService Service, FakeEngine Engine, FakeRegistry Images, FakeWebhook Webhook, DeployEventRing Ring) Build(
        string? latestDigest = NewDigest, bool autoDeploy = true, string? webhookUrl = WebhookUrl, Func<TimeSpan>? redeployTimeout = null) {
        var engine = new FakeEngine();
        var webhook = new FakeWebhook(engine);
        var images = new FakeRegistry(latestDigest);
        var ring = new DeployEventRing();
        var service = new DeployService(Catalog(autoDeploy, webhookUrl), engine, images, webhook, ring, new ZeroDelayTimeProvider(), redeployTimeout);
        return (service, engine, images, webhook, ring);
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

    [Fact]
    public async Task Check_UpToDate_PublishesChecked() {
        var (service, _, _, _, ring) = Build(latestDigest: OldDigest);

        var status = await service.CheckAsync(App, CancellationToken.None);

        Assert.False(status.UpdateAvailable);
        Assert.Equal(OldDigest, status.RunningDigest);
        Assert.Equal("rev-old", status.RunningRevision);
        Assert.Equal([DeployPhase.Checked], Phases(ring));
    }

    [Fact]
    public async Task Check_ContainerRunsDifferentRepo_FailsInsteadOfReportingUpdate() {
        var (service, engine, _, _, ring) = Build();
        engine.Container = engine.Container! with { Image = "ghcr.io/x/other:latest" };

        var status = await service.CheckAsync(App, CancellationToken.None);

        Assert.False(status.UpdateAvailable);
        var failed = Assert.Single(ring.Since(0));
        Assert.Equal(DeployPhase.Failed, failed.Phase);
        Assert.Contains("deploy.apps row", failed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_ContainerRunsDifferentRepo_AbortsBeforePulling() {
        var (service, engine, _, _, ring) = Build();
        engine.Container = engine.Container! with { Image = "ghcr.io/x/other:latest" };

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.DoesNotContain("pull", engine.Calls);
        Assert.Equal([DeployPhase.Failed], Phases(ring));
    }

    [Theory]
    [InlineData("ghcr.io/x/eggledger:latest", null)]
    [InlineData("ghcr.io/x/eggledger@sha256:abc", null)]
    [InlineData("GHCR.io/x/EggLedger:2", null)]
    [InlineData("sha256:0123", null)]
    [InlineData("", null)]
    [InlineData("ghcr.io/x/eggincognito:latest", "ghcr.io/x/eggincognito")]
    public void ImageMismatch_ComparesRepositoryOnly(string running, string? offendingRepo) {
        var container = new ContainerInfo("id", "eggledger", running, OldImageId, [], [], new Dictionary<string, string>(), true);

        var mismatch = DeployService.ImageMismatch(container, ImageRef.Parse("ghcr.io/x/eggledger:latest"));

        if (offendingRepo is null) Assert.Null(mismatch);
        else Assert.Contains(offendingRepo, mismatch, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_NewDigest_PublishesReleaseAvailableOnce() {
        var (service, _, _, _, ring) = Build();

        var first = await service.CheckAsync(App, CancellationToken.None);
        var second = await service.CheckAsync(App, CancellationToken.None);

        Assert.True(first.UpdateAvailable);
        Assert.True(second.UpdateAvailable);
        Assert.Equal(NewDigest, second.LatestDigest);
        Assert.Equal([DeployPhase.ReleaseAvailable, DeployPhase.Checked], Phases(ring));
    }

    [Fact]
    public async Task Check_RegistryFailure_PublishesFailedAndDoesNotThrow() {
        var (service, _, images, _, ring) = Build();
        images.Fail = new HttpRequestException("registry down");

        var status = await service.CheckAsync(App, CancellationToken.None);

        Assert.False(status.UpdateAvailable);
        Assert.Equal([DeployPhase.Failed], Phases(ring));
        Assert.Contains("registry down", ring.Latest(App)!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_UpToDate_PublishesUpToDateAndTouchesNothing() {
        var (service, engine, _, _, ring) = Build(latestDigest: OldDigest);

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.UpToDate], Phases(ring));
        Assert.Equal(["inspect"], engine.Calls);
    }

    [Fact]
    public async Task Deploy_NewImage_RunsFullPhaseSequenceThroughWebhook() {
        var (service, engine, _, webhook, ring) = Build();

        var status = await service.DeployAsync(App, "hook", CancellationToken.None);

        Assert.Equal([DeployPhase.Pulling, DeployPhase.Pulled, DeployPhase.Recreating, DeployPhase.Deployed], Phases(ring));
        Assert.Equal(["inspect", "pull", "inspect-image", "webhook", "inspect"], engine.Calls);
        Assert.Equal([new Uri(WebhookUrl)], webhook.Urls);
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
    }

    [Fact]
    public async Task Deploy_NoWebhookUrl_FailsBeforePulling() {
        var (service, engine, _, webhook, ring) = Build(webhookUrl: null);

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.Failed], Phases(ring));
        Assert.Contains("webhook_url", ring.Latest(App)!.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("pull", engine.Calls);
        Assert.Equal(0, webhook.Calls);
    }

    [Fact]
    public async Task Deploy_WebhookBusy_RetriesThenSucceeds() {
        var (service, _, _, webhook, ring) = Build();
        webhook.BusyRemaining = 2;

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal(3, webhook.Calls);
        Assert.Equal(DeployPhase.Deployed, ring.Latest(App)!.Phase);
    }

    [Fact]
    public async Task Deploy_WebhookFails_PublishesFailed() {
        var (service, engine, _, webhook, ring) = Build();
        webhook.Failure = new InvalidOperationException("portainer webhook returned 500");

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.Pulling, DeployPhase.Pulled, DeployPhase.Recreating, DeployPhase.Failed], Phases(ring));
        Assert.Contains("portainer webhook returned 500", ring.Latest(App)!.Message, StringComparison.Ordinal);
        Assert.Equal(1, webhook.Calls);
        Assert.Equal(["inspect", "pull", "inspect-image", "webhook"], engine.Calls);
    }

    [Fact]
    public async Task Deploy_ContainerNeverComesUp_FailsWithTimeout() {
        var (service, _, _, webhook, ring) = Build(redeployTimeout: () => TimeSpan.FromMilliseconds(1));
        webhook.UpdatesContainer = false;

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal(DeployPhase.Failed, ring.Latest(App)!.Phase);
        Assert.Contains("did not come up", ring.Latest(App)!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_ChangedRow_KeepsInFlightGate() {
        var (service, engine, _, _, ring) = Build();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.PullGate = release.Task;

        var first = service.DeployAsync(App, "manual", CancellationToken.None);
        await engine.PullStarted.Task;
        var diff = service.Apply(new AppCatalog([new DeployApp { Name = App, Image = "ghcr.io/x/eggledger:v2", AutoDeploy = false, WebhookUrl = WebhookUrl }]));
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
    public async Task Deploy_ContainerMissing_PublishesFailed() {
        var (service, engine, _, _, ring) = Build();
        engine.Container = null;

        await service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.Failed], Phases(ring));
        Assert.DoesNotContain("pull", engine.Calls);
    }

    [Fact]
    public async Task Deploy_WhileBusy_ReturnsBusyStatusWithoutSecondRun() {
        var (service, engine, _, _, ring) = Build();
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
        var (service, engine, _, _, ring) = Build();

        await service.TickAsync(CancellationToken.None);

        Assert.Equal(DeployPhase.ReleaseAvailable, ring.Since(0)[0].Phase);
        Assert.Equal(DeployPhase.Deployed, ring.Latest(App)!.Phase);
        Assert.Contains("pull", engine.Calls);
    }

    [Fact]
    public async Task Tick_AutoDeployOff_OnlyAnnounces() {
        var (service, engine, _, _, ring) = Build(autoDeploy: false);

        await service.TickAsync(CancellationToken.None);

        Assert.Equal([DeployPhase.ReleaseAvailable], Phases(ring));
        Assert.DoesNotContain("pull", engine.Calls);
    }

    [Fact]
    public async Task Restart_PublishesRestartingThenCompletion() {
        var (service, engine, _, _, ring) = Build();

        await service.RestartAsync(App, CancellationToken.None);

        Assert.Equal([DeployPhase.Restarting, DeployPhase.Checked], Phases(ring));
        Assert.Contains("restart eggledger", engine.Calls);
    }

    [Fact]
    public void NoteRelease_SurfacesRevisionAndVersionInStatus() {
        var (service, _, _, _, _) = Build();

        service.NoteRelease(App, NewDigest, "rev-hook", "v9");

        var status = service.Status(App)!;
        Assert.Equal(NewDigest, status.LatestDigest);
        Assert.Equal("rev-hook", status.LatestRevision);
        Assert.Equal("v9", status.LatestVersion);
    }

    [Fact]
    public async Task Apply_AddsRemovesAndReplacesAppsAtRuntime() {
        var (service, engine, _, _, ring) = Build();

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
        var (service, _, _, _, ring) = Build();

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

    private sealed class FakeWebhook(FakeEngine engine) : IStackWebhook {
        public int Calls { get; private set; }
        public List<Uri> Urls { get; } = [];
        public int BusyRemaining { get; set; }
        public Exception? Failure { get; set; }
        public bool UpdatesContainer { get; set; } = true;

        public Task InvokeAsync(Uri url, CancellationToken ct) {
            Calls++;
            Urls.Add(url);
            engine.Calls.Add("webhook");
            if (Failure is not null) throw Failure;
            if (BusyRemaining > 0) {
                BusyRemaining--;
                throw new StackBusyException("portainer is already redeploying this stack");
            }
            if (UpdatesContainer && engine.Container is { } current) {
                engine.Container = current with { ImageId = NewImageId, RepoDigests = [$"ghcr.io/x/eggledger@{NewDigest}"], Labels = FakeEngine.Labels("rev-new", "v2") };
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEngine : IDockerEngine {
        public List<string> Calls { get; } = [];
        public ContainerInfo? Container { get; set; } = MakeContainer("old-id", "eggledger", OldDigest, "rev-old", "v1", running: true);
        public Task? PullGate { get; set; }
        public TaskCompletionSource PullStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _pulled;

        public Task<ContainerInfo?> InspectContainerAsync(string name, CancellationToken ct) {
            Calls.Add("inspect");
            return Task.FromResult(Container);
        }

        public Task<ImageInfo?> InspectImageAsync(string reference, CancellationToken ct) {
            Calls.Add("inspect-image");
            var digest = _pulled ? NewDigest : OldDigest;
            var revision = _pulled ? "rev-new" : "rev-old";
            var version = _pulled ? "v2" : "v1";
            var id = _pulled ? NewImageId : OldImageId;
            return Task.FromResult<ImageInfo?>(new ImageInfo(id, [$"ghcr.io/x/eggledger@{digest}"], Labels(revision, version), []));
        }

        public async Task PullImageAsync(string reference, IProgress<string>? progress, CancellationToken ct) {
            Calls.Add("pull");
            PullStarted.TrySetResult();
            if (PullGate is not null) await PullGate;
            progress?.Report("Downloading");
            _pulled = true;
        }

        public Task RestartAsync(string name, CancellationToken ct) {
            Calls.Add($"restart {name}");
            return Task.CompletedTask;
        }

        public Task<string> LogsTailAsync(string name, int lines, CancellationToken ct) {
            Calls.Add($"logs {name}");
            return Task.FromResult("log");
        }

        private static ContainerInfo MakeContainer(string id, string name, string digest, string revision, string version, bool running) =>
            new(id, name, Image, OldImageId, [$"ghcr.io/x/eggledger@{digest}"], [], Labels(revision, version), running);

        public static Dictionary<string, string> Labels(string revision, string version) => new(StringComparer.Ordinal) {
            [OciLabels.Revision] = revision,
            [OciLabels.Version] = version,
        };
    }
}
