namespace EggIdentity.DbClone.Tests;

public class CloneConsoleTests {
    [Theory]
    [InlineData("doctor")]
    [InlineData("plan")]
    [InlineData("verify")]
    public void Parse_AcceptsBareVerbs(string verb) {
        var command = CloneConsole.Parse([verb], out var error);

        Assert.Null(error);
        Assert.Equal(new CloneCommand(verb, false), command);
    }

    [Fact]
    public void Parse_CloneIsDryRunWithoutCommit() {
        Assert.Equal(new CloneCommand("clone", false), CloneConsole.Parse(["clone"], out _));
        Assert.Equal(new CloneCommand("clone", true), CloneConsole.Parse(["clone", "--commit"], out _));
    }

    [Fact]
    public void Parse_RejectsCommitOnOtherVerbs() {
        Assert.Null(CloneConsole.Parse(["verify", "--commit"], out var error));
        Assert.Contains("--commit", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsUnknownVerbAndEmptyArgs() {
        Assert.Null(CloneConsole.Parse(["nuke"], out var unknown));
        Assert.Contains("nuke", unknown, StringComparison.Ordinal);
        Assert.Null(CloneConsole.Parse([], out var empty));
        Assert.Equal(CloneConsole.Usage, empty);
    }

    [Fact]
    public async Task RunAsync_FailsWithoutConnectionVariables() {
        var plan = new ClonePlan("app", "app_subprod", []);

        Assert.Equal(1, await CloneConsole.RunAsync(["plan"], plan, _ => null));
    }
}
