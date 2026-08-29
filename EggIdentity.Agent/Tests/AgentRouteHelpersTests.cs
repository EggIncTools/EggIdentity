namespace EggIdentity.Agent.Tests;

public class AgentRouteHelpersTests {
    [Theory]
    [InlineData(null, 200)]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(2000, 2000)]
    [InlineData(5000, 2000)]
    [InlineData(int.MaxValue, 2000)]
    public void ClampLogLines_ClampsToExpectedRange(int? requested, int expected) {
        Assert.Equal(expected, AgentRouteHelpers.ClampLogLines(requested));
    }
}
