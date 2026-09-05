using System.Text.Json;
using System.Text.Json.Nodes;
using EggIdentity.Resilience;

namespace EggIdentity.Agent;

public sealed record SwapArgs(string OldId, string NewId, string ContainerName);

public static class SwapHelper {
    public const string Command = "swap";
    public const string SocketPath = "/var/run/docker.sock";
    private const string DockerHostPrefix = "DOCKER_HOST=";
    private static readonly TimeSpan StopDeadline = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(30);

    public static bool TryParseArgs(string[] args, out SwapArgs parsed) {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 4 && args[0] == Command && args.Skip(1).All(a => !string.IsNullOrWhiteSpace(a))) {
            parsed = new SwapArgs(args[1], args[2], args[3]);
            return true;
        }
        parsed = null!;
        return false;
    }

    public static string HelperName(string container) => container + "-swap";

    public static ContainerSpec Spec(string image, string container, ContainerInfo old, string newId) {
        ArgumentNullException.ThrowIfNull(old);
        var config = new JsonObject();
        var dockerHost = old.Env.FirstOrDefault(e => e.StartsWith(DockerHostPrefix, StringComparison.Ordinal));
        if (dockerHost is not null) config["Env"] = new JsonArray(dockerHost);
        var empty = DockerJson.ToElement(new JsonObject());
        return new ContainerSpec(HelperName(container), image, DockerJson.ToElement(config), empty, empty) {
            Cmd = [Command, old.Id, newId, container],
            Binds = [SocketBind(old.HostConfig)],
            AutoRemove = true,
            NetworkMode = "none",
        };
    }

    public static async Task<int> RunAsync(IDockerEngine engine, SwapArgs args, TextWriter error) {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(error);
        try {
            await Deadline.RunAsync($"stop {args.OldId}", token => engine.StopAsync(args.OldId, token), StopDeadline);
            await Deadline.RunAsync($"start {args.NewId}", token => engine.StartAsync(args.NewId, token), CallDeadline);
            return 0;
        } catch (Exception e) {
            await error.WriteLineAsync($"swap {args.ContainerName}: {e.Message}");
            await RollbackAsync(engine, args, error);
            return 1;
        }
    }

    private static async Task RollbackAsync(IDockerEngine engine, SwapArgs args, TextWriter error) {
        await QuietAsync($"remove {args.NewId}", token => engine.RemoveAsync(args.NewId, token), error);
        await QuietAsync($"rename {args.OldId}", token => engine.RenameAsync(args.OldId, args.ContainerName, token), error);
        await QuietAsync($"start {args.OldId}", token => engine.StartAsync(args.OldId, token), error);
    }

    private static async Task QuietAsync(string what, Func<CancellationToken, Task> op, TextWriter error) {
        try {
            await Deadline.RunAsync(what, op, CallDeadline);
        } catch (Exception e) {
            await error.WriteLineAsync($"swap rollback: {what}: {e.Message}");
        }
    }

    private static string SocketBind(JsonElement hostConfig) {
        if (hostConfig.ValueKind == JsonValueKind.Object && hostConfig.TryGetProperty("Binds", out var binds) && binds.ValueKind == JsonValueKind.Array) {
            foreach (var bind in binds.EnumerateArray()) {
                var text = bind.GetString();
                if (text is not null && Destination(text) == SocketPath) return text;
            }
        }
        return $"{SocketPath}:{SocketPath}";
    }

    private static string? Destination(string bind) {
        var parts = bind.Split(':');
        return parts.Length >= 2 ? parts[1] : null;
    }
}
