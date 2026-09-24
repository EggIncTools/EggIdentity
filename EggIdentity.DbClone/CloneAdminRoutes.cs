using EggIdentity.Contract;
using EggIdentity.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EggIdentity.DbClone;

public static class CloneAdminRoutes {
    public static RouteGroupBuilder MapCloneAdminApi(this RouteGroupBuilder group, ClonePlan plan) {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(plan);
        var run = new CloneRun(plan);

        group.MapPost("/clone", ([FromServices] IServiceProvider services) => {
            if (CloneRun.Refusal(services) is { } refusal)
                return Results.Ok(new AdminSaveResponse { Ok = false, Error = refusal });
            return run.TryStart()
                ? Results.Ok(new AdminSaveResponse { Ok = true })
                : Results.Json(
                    new AdminSaveResponse { Ok = false, Error = "a clone is already running" },
                    statusCode: StatusCodes.Status409Conflict);
        });

        group.MapGet("/clone", async (CancellationToken ct) => Results.Ok(await run.StatusAsync(ct)));

        return group;
    }

    internal static string ResolveEnvironment(IServiceProvider services) {
        if (services.GetService<ISettingsSource>() is { } source) {
            try {
                var value = source.Value(EnvironmentSettings.Key).Value;
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            } catch (KeyNotFoundException) {
                return EnvironmentSettings.Resolve(Environment.GetEnvironmentVariable);
            }
        }
        return EnvironmentSettings.Resolve(Environment.GetEnvironmentVariable);
    }

    private sealed class CloneRun(ClonePlan plan) {
        private readonly CloneTracker _tracker = new();
        private int _active;

        public static string? Refusal(IServiceProvider services) {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CloneConsole.SourceEnvKey)))
                return $"{CloneConsole.SourceEnvKey} is not set on this host, so it cannot be a clone target";
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CloneConsole.TargetEnvKey)))
                return $"{CloneConsole.TargetEnvKey} is not set on this host";
            var environment = ResolveEnvironment(services);
            return EnvironmentSettings.IsSubProd(environment)
                ? null
                : $"{EnvironmentSettings.Key} is \"{environment}\", only {EnvironmentSettings.SubProd} accepts a clone";
        }

        public bool TryStart() {
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) return false;
            var source = Environment.GetEnvironmentVariable(CloneConsole.SourceEnvKey)!;
            var target = Environment.GetEnvironmentVariable(CloneConsole.TargetEnvKey)!;
            _tracker.Start();
            _ = Task.Run(() => ExecuteAsync(source, target), CancellationToken.None);
            return true;
        }

        public async Task<CloneStatusResponse> StatusAsync(CancellationToken ct) {
            var status = _tracker.Snapshot();
            CloneStamp? stamp = null;
            var target = Environment.GetEnvironmentVariable(CloneConsole.TargetEnvKey);
            if (!string.IsNullOrWhiteSpace(target)) {
                try {
                    stamp = await CloneRunner.LastStampAsync(target, ct);
                } catch (Exception e) when (e is Npgsql.NpgsqlException or InvalidOperationException or ArgumentException) {
                    stamp = null;
                }
            }
            return CloneWire.ToWire(plan.App, status, stamp);
        }

        private async Task ExecuteAsync(string source, string target) {
            try {
                var counts = await CloneRunner.RunAsync(plan, source, target, false, _tracker, CancellationToken.None);
                _tracker.Finish(counts);
            } catch (Exception e) {
                _tracker.Fail(e.Message);
            } finally {
                Interlocked.Exchange(ref _active, 0);
            }
        }
    }
}
