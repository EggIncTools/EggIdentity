using EggIdentity.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace EggIdentity.Reports.Browser.Tests;

public class BrowserFallbackScopeTests {
    private sealed record Drop(string Ship, double Amount);

    private sealed class Recorder {
        public List<(string? Partition, string? User)> Reads { get; } = [];
    }

    private sealed class Viewer {
        public string? Name { get; set; }
    }

    private sealed class RecordingProvider(Recorder recorder, Viewer viewer) : IReportRowProvider<Drop> {
        public ValueTask<IReadOnlyList<Drop>> ReadAsync(string? partition, CancellationToken ct) {
            recorder.Reads.Add((partition, viewer.Name));
            IReadOnlyList<Drop> rows = [new("henerprise", 5)];
            return ValueTask.FromResult(rows);
        }

        public ValueTask<string> VersionAsync(string? partition, CancellationToken ct) => ValueTask.FromResult("v1");
    }

    private sealed class UnavailableJs : IJSRuntime {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new JSException("unavailable: no worker");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new JSException("unavailable: no worker");
    }

    private static readonly ReportDimension Ship = new(new FilterFieldDef("ship", "Ship", FilterValueKind.Select, FilterOps.Equality));

    private static readonly ReportMeasure Value = new("value", "Value");

    private static readonly ReportSource<Drop> Source = new(
        "drops", "Drops", [Ship], [Value], (d, key) => key switch {
            "ship" => FilterSubjectValue.Of(d.Ship),
            "value" => FilterSubjectValue.Of(d.Amount),
            _ => FilterSubjectValue.Absent,
        });

    private static readonly SnapshotSchema Schema = new("drops", [new("ship")], ["value"]);

    private static ServiceProvider Build(Recorder recorder) {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(recorder);
        services.AddScoped<Viewer>();
        services.AddScoped<IJSRuntime, UnavailableJs>();
        services.AddReportSource<Drop, RecordingProvider>(Source, Schema);
        services.AddEggIdentityBrowserReports(o => o.ScopeKey = _ => "alice");
        return services.BuildServiceProvider();
    }

    private static BrowserReportRegistry Circuit(AsyncServiceScope scope) {
        scope.ServiceProvider.GetRequiredService<Viewer>().Name = "alice";
        return scope.ServiceProvider.GetRequiredService<BrowserReportRegistry>();
    }

    [Theory]
    [InlineData(ReportTier.Live)]
    [InlineData(ReportTier.Snapshot)]
    public async Task Fallback_PassesPartitionAndCircuitUser(ReportTier tier) {
        var recorder = new Recorder();
        await using var root = Build(recorder);
        await using var scope = root.CreateAsyncScope();
        var reports = Circuit(scope);

        await reports.Registry.RunAsync(new ReportQuery("drops"), new ReportContext(TimeZoneInfo.Utc, tier, "a"), CancellationToken.None);

        Assert.False(reports.BrowserActive);
        Assert.Equal([("a", "alice")], recorder.Reads);
    }

    [Fact]
    public async Task Fallback_SnapshotKeepsPartitionsApart() {
        var recorder = new Recorder();
        await using var root = Build(recorder);
        await using var scope = root.CreateAsyncScope();
        var reports = Circuit(scope);

        await reports.Registry.RunAsync(new ReportQuery("drops"), new ReportContext(TimeZoneInfo.Utc, ReportTier.Snapshot, "a"), CancellationToken.None);
        await reports.Registry.RunAsync(new ReportQuery("drops"), new ReportContext(TimeZoneInfo.Utc, ReportTier.Snapshot, "b"), CancellationToken.None);
        await reports.Registry.RunAsync(new ReportQuery("drops"), new ReportContext(TimeZoneInfo.Utc, ReportTier.Snapshot, "a"), CancellationToken.None);

        Assert.Equal(["a", "b"], recorder.Reads.Select(r => r.Partition));
    }
}
