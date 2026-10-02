using EggIdentity.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace EggIdentity.Testing.Tests;

public class PeriodicScopedServiceTests {
    private sealed class Probe(IServiceScopeFactory scopes, TimeProvider time, bool runAtStart, bool enabled = true, bool fail = false)
        : PeriodicScopedService(scopes, time, NullLogger.Instance) {
        public int Runs;

        protected override bool Enabled => enabled;

        protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

        protected override bool RunAtStart => runAtStart;

        protected override Task RunOnceAsync(IServiceProvider services, CancellationToken ct) {
            Interlocked.Increment(ref Runs);
            return fail ? throw new InvalidOperationException("boom") : Task.CompletedTask;
        }

        public Task Loop(CancellationToken ct) => ExecuteAsync(ct);
    }

    private static IServiceScopeFactory Scopes() => new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private static async Task Settle(Func<bool> done) {
        for (var i = 0; i < 400 && !done(); i++) await Task.Delay(5);
    }

    private static async Task Stop(Task loop, CancellationTokenSource cts) {
        await cts.CancelAsync();
        await loop;
    }

    [Theory]
    [InlineData(true, 1, 3)]
    [InlineData(false, 0, 2)]
    public async Task RunsAtStartOnlyWhenAsked_ThenOncePerInterval(bool runAtStart, int atStart, int afterTwoTicks) {
        var time = new FakeTimeProvider();
        var probe = new Probe(Scopes(), time, runAtStart);
        using var cts = new CancellationTokenSource();

        var loop = probe.Loop(cts.Token);
        Assert.Equal(atStart, probe.Runs);

        time.Advance(TimeSpan.FromMinutes(5));
        await Settle(() => probe.Runs >= atStart + 1);
        time.Advance(TimeSpan.FromMinutes(5));
        await Settle(() => probe.Runs >= afterTwoTicks);
        await Stop(loop, cts);

        Assert.Equal(afterTwoTicks, probe.Runs);
    }

    [Fact]
    public async Task FailingTickIsLoggedAndTheLoopContinues() {
        var time = new FakeTimeProvider();
        var probe = new Probe(Scopes(), time, true, fail: true);
        using var cts = new CancellationTokenSource();

        var loop = probe.Loop(cts.Token);
        time.Advance(TimeSpan.FromMinutes(5));
        await Settle(() => probe.Runs >= 2);
        await Stop(loop, cts);

        Assert.Equal(2, probe.Runs);
    }

    [Fact]
    public async Task DisabledNeverRuns() {
        var probe = new Probe(Scopes(), new FakeTimeProvider(), true, enabled: false);

        await probe.Loop(CancellationToken.None);

        Assert.Equal(0, probe.Runs);
    }
}
