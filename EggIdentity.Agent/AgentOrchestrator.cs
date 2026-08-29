using EggIdentity.Contract;

namespace EggIdentity.Agent;

public sealed class AgentOrchestrator(AgentRegistry registry, TimeSpan tickInterval, string notifySecret) {
    private readonly Dictionary<string, DeployHandler> _handlers = registry.Apps.Keys.ToDictionary(
        k => k, _ => new DeployHandler());
    private readonly Dictionary<string, Executor> _slowExecutors = registry.Apps.ToDictionary(
        kv => kv.Key,
        kv => new Executor { Repo = kv.Value.Repo, RepoUrl = kv.Value.RepoUrl, Steps = kv.Value.Steps });
    private readonly Dictionary<string, Executor> _fastExecutors = registry.Apps
        .Where(kv => kv.Value.FastSteps.Count > 0)
        .ToDictionary(
            kv => kv.Key,
            kv => new Executor { Repo = kv.Value.Repo, RepoUrl = kv.Value.RepoUrl, Steps = kv.Value.FastSteps });
    private readonly Dictionary<string, Watcher> _watchers = registry.Apps.ToDictionary(
        kv => kv.Key,
        kv => new Watcher(kv.Key, kv.Value.Watch?.NotifyBotUrl ?? "", notifySecret));

    public bool HasFastPipeline(string appName) => _fastExecutors.ContainsKey(appName);

    public async Task<(DeployResponse Result, bool Ran)> TryDeployAsync(string appName) {
        var handler = _handlers[appName];
        var executor = _slowExecutors[appName];
        if (!handler.TryEnter()) return (new DeployResponse(), false);
        var res = await Task.Run(() => handler.RunAndExit(executor.Run));
        return (res, true);
    }

    public async Task<(DeployResponse Result, bool Ran)> TryDeployFastAsync(string appName) {
        var handler = _handlers[appName];
        var executor = _fastExecutors[appName];
        if (!handler.TryEnter()) return (new DeployResponse(), false);
        var res = await Task.Run(() => handler.RunAndExit(executor.Run));
        return (res, true);
    }

    public async Task RunAsync(CancellationToken ct) {
        using var timer = new PeriodicTimer(tickInterval);
        try {
            while (await timer.WaitForNextTickAsync(ct))
                await Task.WhenAll(registry.Apps.Keys.Select(TickOneAsync));
        } catch (OperationCanceledException) { }
    }

    private async Task TickOneAsync(string appName) {
        Console.WriteLine($"orchestrator: tick: {appName}: checking for updates");
        var (res, ran) = await TryDeployAsync(appName);
        if (!ran) {
            Console.WriteLine($"orchestrator: tick: {appName}: skipped, deploy already in progress");
            return;
        }
        _watchers[appName].HandleResult(res);
    }
}
