using System.Text.Json;

namespace EggIdentity.Agent.Tests;

public class SwapHelperTests {
    private static readonly SwapArgs Args = new("old-id", "new-id", "eggidentity-agent");

    [Fact]
    public void TryParseArgs_AcceptsSwapWithThreeOperands() {
        Assert.True(SwapHelper.TryParseArgs(["swap", "old", "new", "name"], out var parsed));
        Assert.Equal(new SwapArgs("old", "new", "name"), parsed);
    }

    [Fact]
    public void TryParseArgs_RejectsAnythingElse() {
        string[][] cases = [[], ["swap"], ["swap", "old", "new"], ["swap", "old", "", "name"], ["other", "old", "new", "name"]];
        foreach (var args in cases) Assert.False(SwapHelper.TryParseArgs(args, out _));
    }

    [Fact]
    public async Task RunAsync_StopsOldThenStartsNew_LeavesOldForReap() {
        var engine = new RecordingEngine();
        var error = new StringWriter();

        var code = await SwapHelper.RunAsync(engine, Args, error);

        Assert.Equal(0, code);
        Assert.Equal(["stop old-id", "start new-id"], engine.Calls);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public async Task RunAsync_StartFails_RemovesNewRenamesAndRestartsOld() {
        var engine = new RecordingEngine { StartFailures = { ["new-id"] = new InvalidOperationException("no such image") } };
        var error = new StringWriter();

        var code = await SwapHelper.RunAsync(engine, Args, error);

        Assert.Equal(1, code);
        Assert.Equal(["stop old-id", "start new-id", "remove new-id", "rename old-id -> eggidentity-agent", "start old-id"], engine.Calls);
        Assert.Contains("no such image", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_StartFailsAndRollbackPartiallyFails_StillTriesEveryStep() {
        var engine = new RecordingEngine {
            StartFailures = { ["new-id"] = new InvalidOperationException("boom"), ["old-id"] = new InvalidOperationException("old gone") },
            RemoveFailure = new InvalidOperationException("remove refused"),
        };
        var error = new StringWriter();

        var code = await SwapHelper.RunAsync(engine, Args, error);

        Assert.Equal(1, code);
        Assert.Equal(["stop old-id", "start new-id", "remove new-id", "rename old-id -> eggidentity-agent", "start old-id"], engine.Calls);
        Assert.Contains("remove refused", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("old gone", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Spec_BuildsDetachedHelperFromOldContainer() {
        var old = Container(
            ["DOCKER_HOST=tcp://docker:2375", "AGENT_PORT=7777"],
            """{ "Binds": ["/data:/data", "/var/run/docker.sock:/var/run/docker.sock"], "RestartPolicy": { "Name": "always" } }""");

        var spec = SwapHelper.Spec("ghcr.io/x/agent:2", "eggidentity-agent", old, "new-id");

        Assert.Equal("eggidentity-agent-swap", spec.Name);
        Assert.Equal("ghcr.io/x/agent:2", spec.Image);
        Assert.Equal(["swap", "old-id", "new-id", "eggidentity-agent"], spec.Cmd);
        Assert.Equal(["/var/run/docker.sock:/var/run/docker.sock"], spec.Binds);
        Assert.True(spec.AutoRemove);
        Assert.Equal("none", spec.NetworkMode);
        Assert.Equal(["DOCKER_HOST=tcp://docker:2375"], spec.Config.GetProperty("Env").EnumerateArray().Select(e => e.GetString()));
        Assert.False(spec.HostConfig.TryGetProperty("RestartPolicy", out _));
    }

    [Fact]
    public void Spec_NoSocketBindAndNoDockerHost_UsesDefaultsWithoutEnv() {
        var old = Container(["AGENT_PORT=7777"], """{ "Binds": ["/data:/data"] }""");

        var spec = SwapHelper.Spec("img", "agent", old, "new-id");

        Assert.Equal(["/var/run/docker.sock:/var/run/docker.sock"], spec.Binds);
        Assert.False(spec.Config.TryGetProperty("Env", out _));
    }

    private static ContainerInfo Container(string[] env, string hostConfig) {
        using var empty = JsonDocument.Parse("{}");
        using var host = JsonDocument.Parse(hostConfig);
        return new ContainerInfo("old-id", "eggidentity-agent", "img", "sha256:img", [], env, new Dictionary<string, string>(), true,
            empty.RootElement.Clone(), host.RootElement.Clone(), empty.RootElement.Clone());
    }

    private sealed class RecordingEngine : IDockerEngine {
        public List<string> Calls { get; } = [];
        public Dictionary<string, Exception> StartFailures { get; } = new(StringComparer.Ordinal);
        public Exception? RemoveFailure { get; set; }

        public Task<ContainerInfo?> InspectContainerAsync(string name, CancellationToken ct) => Task.FromResult<ContainerInfo?>(null);
        public Task<ImageInfo?> InspectImageAsync(string reference, CancellationToken ct) => Task.FromResult<ImageInfo?>(null);
        public Task PullImageAsync(string reference, IProgress<string>? progress, CancellationToken ct) => Task.CompletedTask;
        public Task<string> CreateAsync(ContainerSpec spec, CancellationToken ct) => Task.FromResult("id");
        public Task RestartAsync(string name, CancellationToken ct) => Task.CompletedTask;
        public Task<string> LogsTailAsync(string name, int lines, CancellationToken ct) => Task.FromResult("");

        public Task RenameAsync(string name, string newName, CancellationToken ct) {
            Calls.Add($"rename {name} -> {newName}");
            return Task.CompletedTask;
        }

        public Task StartAsync(string name, CancellationToken ct) {
            Calls.Add($"start {name}");
            return StartFailures.TryGetValue(name, out var failure) ? Task.FromException(failure) : Task.CompletedTask;
        }

        public Task StopAsync(string name, CancellationToken ct) {
            Calls.Add($"stop {name}");
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string name, CancellationToken ct) {
            Calls.Add($"remove {name}");
            return RemoveFailure is null ? Task.CompletedTask : Task.FromException(RemoveFailure);
        }
    }
}
