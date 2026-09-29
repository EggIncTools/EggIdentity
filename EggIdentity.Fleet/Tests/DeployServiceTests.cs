using EggIdentity.Contract;
using EggIdentity.Deploy;

namespace EggIdentity.Fleet.Tests;

public class DeployServiceTests {
    private const string App = "eggledger";
    private const string Image = "ghcr.io/x/eggledger:latest";
    private const string OldDigest = "sha256:old";
    private const string NewDigest = "sha256:new";
    private const string OldImageId = "sha256:img";
    private const string NewImageId = "sha256:img-new";
    private const string StackName = "egg-apps";

    private sealed record Rig(DeployService Service, FakeEngine Engine, FakePortainer Portainer, FakeRegistry Images, DeployEventRing Ring);

    private static Rig Build(
        string? latestDigest = NewDigest, string? stack = StackName, bool portainer = true,
        Func<TimeSpan>? redeployTimeout = null, params PortainerStack[] stacks) {
        var engine = new FakeEngine();
        var fake = new FakePortainer(engine, stacks.Length > 0 ? stacks : [PortainerFakes.Parse(PortainerFakes.GitStack)]);
        var images = new FakeRegistry(latestDigest);
        var ring = new DeployEventRing(firstId: 1);
        IReadOnlyList<SuiteApp> rows = [new SuiteApp { Name = App, Stack = stack }, new SuiteApp { Name = "off", Stack = StackName, Enabled = false }];
        var service = new DeployService(_ => Task.FromResult(rows), portainer ? fake : null, images, ring, new ZeroDelayTimeProvider(), redeployTimeout);
        return new Rig(service, engine, fake, images, ring);
    }

    private static IEnumerable<DeployPhase> Phases(DeployEventRing ring) => ring.Since(0).Select(e => e.Phase);

    private static string LastMessage(DeployEventRing ring) => ring.Latest(App)!.Message;

    [Fact]
    public async Task Check_UpToDate_PublishesCheckedAndStampsRunningState() {
        var rig = Build(latestDigest: OldDigest);

        var status = await rig.Service.CheckAsync(App, CancellationToken.None);

        Assert.NotNull(status);
        Assert.False(status.UpdateAvailable);
        Assert.Equal("rev-old", status.RunningRevision);
        Assert.Equal(Image, status.Image);
        Assert.Equal(StackName, status.Stack);
        Assert.True(status.Running);
        Assert.Equal([DeployPhase.Checked], Phases(rig.Ring));
    }

    [Fact]
    public async Task Check_NewDigest_AnnouncesReleaseOnce() {
        var rig = Build();

        await rig.Service.CheckAsync(App, CancellationToken.None);
        var second = await rig.Service.CheckAsync(App, CancellationToken.None);

        Assert.True(second!.UpdateAvailable);
        Assert.Equal([DeployPhase.ReleaseAvailable, DeployPhase.Checked], Phases(rig.Ring));
    }

    [Fact]
    public async Task Check_RegistryFailure_PublishesFailed() {
        var rig = Build();
        rig.Images.Fail = new HttpRequestException("registry down");

        await rig.Service.CheckAsync(App, CancellationToken.None);

        Assert.Equal([DeployPhase.Failed], Phases(rig.Ring));
        Assert.Contains("registry down", LastMessage(rig.Ring), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_UntaggedImage_ReportsInsteadOfThrowing() {
        var rig = Build();
        rig.Engine.Container = rig.Engine.Container! with { Image = "sha256:abcdef" };

        var status = await rig.Service.CheckAsync(App, CancellationToken.None);

        Assert.False(status!.UpdateAvailable);
        Assert.Contains("untagged image", LastMessage(rig.Ring), StringComparison.Ordinal);
        Assert.Equal(0, rig.Images.Calls);
    }

    [Fact]
    public async Task UnknownOrDisabledApp_IsNull() {
        var rig = Build();

        Assert.Null(await rig.Service.CheckAsync("ghost", CancellationToken.None));
        Assert.Null(await rig.Service.DeployAsync("off", "manual", CancellationToken.None));
        Assert.Null(await rig.Service.StatusAsync("off", CancellationToken.None));
    }

    [Fact]
    public async Task Deploy_NewImage_RunsFullPhaseSequence() {
        var rig = Build();

        var status = await rig.Service.DeployAsync(App, "hook", CancellationToken.None);

        Assert.Equal([DeployPhase.Pulling, DeployPhase.Pulled, DeployPhase.Recreating, DeployPhase.Deployed], Phases(rig.Ring));
        Assert.Equal(["inspect", "pull " + Image, "inspect-image", "inspect"], rig.Engine.Calls);
        Assert.Equal([StackName], rig.Portainer.Redeployed);
        Assert.Equal(NewDigest, status!.RunningDigest);
        Assert.Equal("rev-new", status.RunningRevision);
        Assert.False(status.UpdateAvailable);
        Assert.False(status.Busy);
        Assert.Equal(9, rig.Portainer.DockerEndpoint);
    }

    [Fact]
    public async Task Deploy_UpToDate_TouchesNothing() {
        var rig = Build(latestDigest: OldDigest);

        await rig.Service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.UpToDate], Phases(rig.Ring));
        Assert.Empty(rig.Portainer.Redeployed);
    }

    [Fact]
    public async Task Deploy_RowWithoutStack_RefusesAndRecordsProblem() {
        var rig = Build(stack: null);

        var status = await rig.Service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.Failed], Phases(rig.Ring));
        Assert.Contains("names no Portainer stack", status!.Problem, StringComparison.Ordinal);
        Assert.DoesNotContain(rig.Engine.Calls, c => c.StartsWith("pull", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Deploy_AmbiguousStack_RefusesNamingBothIds() {
        var rig = Build(stacks: [
            PortainerFakes.Parse(PortainerFakes.GitStack),
            PortainerFakes.Parse(PortainerFakes.WebEditorStack(id: 71, name: StackName)),
        ]);

        await rig.Service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Contains("56", LastMessage(rig.Ring), StringComparison.Ordinal);
        Assert.Contains("71", LastMessage(rig.Ring), StringComparison.Ordinal);
        Assert.Empty(rig.Portainer.Redeployed);
    }

    [Fact]
    public async Task Deploy_StackNotReady_RefusesBeforePulling() {
        var rig = Build(stacks: [PortainerFakes.Parse(PortainerFakes.GitStack.Replace("\"ForceUpdate\":true", "\"ForceUpdate\":false", StringComparison.Ordinal))]);

        await rig.Service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Contains("force redeploy", LastMessage(rig.Ring), StringComparison.Ordinal);
        Assert.Empty(rig.Engine.Calls);
    }

    [Fact]
    public async Task Deploy_RedeployBusy_RetriesThenSucceeds() {
        var rig = Build();
        rig.Portainer.BusyRemaining = 2;

        await rig.Service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal(3, rig.Portainer.RedeployCalls);
        Assert.Equal(DeployPhase.Deployed, rig.Ring.Latest(App)!.Phase);
    }

    [Fact]
    public async Task Deploy_RedeployFails_PublishesFailed() {
        var rig = Build();
        rig.Portainer.Failure = new InvalidOperationException("portainer webhook returned 500");

        await rig.Service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Equal([DeployPhase.Pulling, DeployPhase.Pulled, DeployPhase.Recreating, DeployPhase.Failed], Phases(rig.Ring));
        Assert.Contains("500", LastMessage(rig.Ring), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_ContainerNeverComesUp_FailsWithTimeout() {
        var rig = Build(redeployTimeout: () => TimeSpan.FromMilliseconds(1));
        rig.Portainer.UpdatesContainer = false;

        await rig.Service.DeployAsync(App, "manual", CancellationToken.None);

        Assert.Contains("did not come up", LastMessage(rig.Ring), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deploy_WhileBusy_ReturnsBusyWithoutSecondRun() {
        var rig = Build();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Engine.PullGate = release.Task;

        var first = rig.Service.DeployAsync(App, "manual", CancellationToken.None);
        await rig.Engine.PullStarted.Task;
        var second = await rig.Service.DeployAsync(App, "manual", CancellationToken.None);
        release.SetResult();
        await first;

        Assert.True(second!.Busy);
        Assert.Single(rig.Engine.Calls, c => c.StartsWith("pull", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Restart_GoesThroughDockerProxy() {
        var rig = Build();

        var failure = await rig.Service.RestartAsync(App, CancellationToken.None);

        Assert.Null(failure);
        Assert.Contains("restart " + App, rig.Engine.Calls);
        Assert.Equal([DeployPhase.Restarting, DeployPhase.Checked], Phases(rig.Ring));
    }

    [Fact]
    public async Task Env_BuildsProvenanceFromContainerImageComposeAndStack() {
        var rig = Build();
        rig.Engine.Container = rig.Engine.Container! with { Env = ["A=1", "PATH=/bin"] };
        rig.Portainer.ComposeFile = "services:\n  eggledger:\n    environment:\n      - A=${A}\n";

        var (env, refusal) = await rig.Service.EnvAsync(App, CancellationToken.None);

        Assert.Null(refusal);
        Assert.Contains(env!, e => e.Name == "A" && e.Origin == Settings.EnvOrigin.ServiceEnvironment);
        Assert.Contains(env!, e => e.Name == "PATH" && e.Origin == Settings.EnvOrigin.Image);
        Assert.Contains(env!, e => e.Name == "B" && e.Origin == Settings.EnvOrigin.StackVariable && !e.Referenced);
    }

    [Fact]
    public async Task PatchEnv_UpdatesTheAppsStack() {
        var rig = Build();

        var failure = await rig.Service.PatchEnvAsync(App, new Dictionary<string, string?> { ["A"] = "2" }, CancellationToken.None);

        Assert.Null(failure);
        Assert.Equal([StackName], rig.Portainer.EnvUpdated);
    }

    [Fact]
    public async Task Stacks_ListsEveryReferencedStackOnce() {
        var rig = Build(stacks: [PortainerFakes.Parse(PortainerFakes.GitStack)]);

        var stacks = await rig.Service.StacksAsync(CancellationToken.None);

        var only = Assert.Single(stacks);
        Assert.True(only.Ready);
    }

    [Fact]
    public async Task PortainerStacks_ListsUnreferencedStacksToo() {
        var rig = Build(stacks: [PortainerFakes.Parse(PortainerFakes.GitStack), PortainerFakes.Parse(PortainerFakes.WebEditorStack())]);

        var (stacks, refusal) = await rig.Service.PortainerStacksAsync(CancellationToken.None);

        Assert.Null(refusal);
        Assert.Equal(["db", StackName], stacks!.Select(s => s.Name));
    }

    [Fact]
    public async Task StackServices_ParsesTheStackFile_AndRefusesUnknownStacks() {
        var rig = Build();
        rig.Portainer.ComposeFile = "services:\n  eggledger:\n    image: ghcr.io/x/eggledger:latest\n";

        var (services, _) = await rig.Service.StackServicesAsync(StackName, CancellationToken.None);
        var (missing, refusal) = await rig.Service.StackServicesAsync("nope", CancellationToken.None);

        Assert.Equal(["eggledger"], services!.Select(s => s.Service));
        Assert.Null(missing);
        Assert.Contains("no stack named", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StackServices_WithoutPortainer_Refuses() {
        var rig = Build(portainer: false);

        var (_, refusal) = await rig.Service.StackServicesAsync(StackName, CancellationToken.None);

        Assert.Equal(DeployService.PortainerMissing, refusal);
    }

    [Fact]
    public async Task RedeployStack_UnreferencedStack_Refuses() {
        var rig = Build();

        Assert.Contains("no suite.apps row", await rig.Service.RedeployStackAsync("db", CancellationToken.None), StringComparison.Ordinal);
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

    private sealed class FakeRegistry(string? digest) : IImageRegistry {
        public Exception? Fail { get; set; }
        public int Calls { get; private set; }

        public Task<string> GetDigestAsync(ImageRef image, CancellationToken ct) {
            Calls++;
            return Fail is not null ? throw Fail : Task.FromResult(digest ?? throw new InvalidOperationException("no digest"));
        }
    }

    private sealed class FakePortainer(FakeEngine engine, IReadOnlyList<PortainerStack> stacks) : IPortainer {
        public List<string> Redeployed { get; } = [];
        public List<string> EnvUpdated { get; } = [];
        public int RedeployCalls { get; private set; }
        public int BusyRemaining { get; set; }
        public Exception? Failure { get; set; }
        public bool UpdatesContainer { get; set; } = true;
        public string ComposeFile { get; set; } = "services: {}";
        public int? DockerEndpoint { get; private set; }

        public Task<IReadOnlyList<PortainerStack>> ListStacksAsync(CancellationToken ct) => Task.FromResult(stacks);

        public Task<string> GetStackFileAsync(PortainerStack stack, CancellationToken ct) => Task.FromResult(ComposeFile);

        public Task RedeployAsync(PortainerStack stack, CancellationToken ct) {
            RedeployCalls++;
            if (Failure is not null) throw Failure;
            if (BusyRemaining > 0) {
                BusyRemaining--;
                throw new StackBusyException("portainer is already redeploying this stack");
            }
            Redeployed.Add(stack.Name);
            if (UpdatesContainer && engine.Container is { } current)
                engine.Container = current with { ImageId = NewImageId, RepoDigests = [$"ghcr.io/x/eggledger@{NewDigest}"], Labels = FakeEngine.Labels("rev-new", "v2") };
            return Task.CompletedTask;
        }

        public Task UpdateEnvAsync(PortainerStack stack, IReadOnlyDictionary<string, string?> changes, CancellationToken ct) {
            EnvUpdated.Add(stack.Name);
            return Task.CompletedTask;
        }

        public IDockerEngine Docker(int endpointId) {
            DockerEndpoint = endpointId;
            return engine;
        }
    }

    private sealed class FakeEngine : IDockerEngine {
        public List<string> Calls { get; } = [];
        public ContainerInfo? Container { get; set; } = new(
            "id", App, Image, OldImageId, [$"ghcr.io/x/eggledger@{OldDigest}"], [], Labels("rev-old", "v1"), true);
        public Task? PullGate { get; set; }
        public TaskCompletionSource PullStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _pulled;

        public Task<ContainerInfo?> InspectContainerAsync(string name, CancellationToken ct) {
            Calls.Add("inspect");
            return Task.FromResult(Container);
        }

        public Task<ImageInfo?> InspectImageAsync(string reference, CancellationToken ct) {
            Calls.Add("inspect-image");
            return Task.FromResult<ImageInfo?>(_pulled
                ? new ImageInfo(NewImageId, [$"ghcr.io/x/eggledger@{NewDigest}"], Labels("rev-new", "v2"), [])
                : new ImageInfo(OldImageId, [$"ghcr.io/x/eggledger@{OldDigest}"], Labels("rev-old", "v1"), ["PATH=/bin"]));
        }

        public async Task PullImageAsync(string reference, IProgress<string>? progress, CancellationToken ct) {
            Calls.Add("pull " + reference);
            PullStarted.TrySetResult();
            if (PullGate is not null) await PullGate;
            _pulled = true;
        }

        public Task RestartAsync(string name, CancellationToken ct) {
            Calls.Add($"restart {name}");
            return Task.CompletedTask;
        }

        public Task<string> LogsTailAsync(string name, int lines, CancellationToken ct) => Task.FromResult("log");

        public static Dictionary<string, string> Labels(string revision, string version) => new(StringComparer.Ordinal) {
            [OciLabels.Revision] = revision,
            [OciLabels.Version] = version,
        };
    }
}
