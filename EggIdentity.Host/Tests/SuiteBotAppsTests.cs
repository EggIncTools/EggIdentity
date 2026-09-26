using EggIdentity.Contract;
using EggIdentity.Deploy;

namespace EggIdentity.Host.Tests;

public class SuiteBotAppsTests {
    private static readonly DateTimeOffset Started = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static DeployStatus Status(bool updateAvailable = false, bool busy = false, bool? running = true, string? problem = null) =>
        new("egginctools", "sha256:a", "abc1234def", "3.1.0", null, null, null, updateAvailable, null,
            new DeployEvent(1, "egginctools", DeployPhase.Deployed, "deployed", Started, null, null, null, null), busy) {
            Running = running,
            StartedAt = Started,
            Problem = problem,
        };

    [Fact]
    public void Snapshot_MapsLabelVersionRevisionStartAndRepo() {
        var app = new SuiteApp { Name = "egginctools", DisplayName = "EggIncTools", RepoUrl = "https://github.com/EggIncTools/egginctools" };

        var snapshot = SuiteBotApps.Snapshot(app, Status());

        Assert.Equal("EggIncTools", snapshot.AppName);
        Assert.Equal("3.1.0", snapshot.Version);
        Assert.Equal("abc1234def", snapshot.BuildHash);
        Assert.Equal(Started, snapshot.UptimeSince);
        Assert.Equal("https://github.com/EggIncTools/egginctools", snapshot.RepoUrl);
        Assert.Equal("Deployed", snapshot.DeployStatus);
    }

    [Fact]
    public void Snapshot_WithoutStatus_IsUnknown() =>
        Assert.Equal("unknown", SuiteBotApps.Snapshot(new SuiteApp { Name = "x" }, null).DeployStatus);

    [Fact]
    public void DeployText_PrefersProblemThenBusyThenStoppedThenUpdate() {
        Assert.Equal("no stack", SuiteBotApps.DeployText(Status(busy: true, problem: "no stack")));
        Assert.Equal("deploying", SuiteBotApps.DeployText(Status(busy: true, updateAvailable: true)));
        Assert.Equal("stopped", SuiteBotApps.DeployText(Status(running: false, updateAvailable: true)));
        Assert.Equal("update available", SuiteBotApps.DeployText(Status(updateAvailable: true)));
    }
}
