using EggIdentity.Contract;

namespace EggIdentity.Agent.Tests;

public class ServerSentEventsTests {
    [Fact]
    public void Format_EmitsIdEventAndJsonData() {
        var evt = new DeployEvent(7, "eggledger", DeployPhase.Pulled, "pulled abc1234", new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero), "abc", "def", "v2", "sha256:1");

        var text = ServerSentEvents.Format(evt);

        Assert.StartsWith("id: 7\nevent: deploy\ndata: {", text, StringComparison.Ordinal);
        Assert.EndsWith("}\n\n", text, StringComparison.Ordinal);
        Assert.Contains("\"phase\":\"Pulled\"", text, StringComparison.Ordinal);
        Assert.Contains("\"app\":\"eggledger\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n", text[..^2], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("12", null, 12)]
    [InlineData(null, "5", 5)]
    [InlineData("12", "5", 12)]
    [InlineData(null, null, 0)]
    [InlineData("junk", "3", 3)]
    [InlineData("-4", null, 0)]
    [InlineData(" 9 ", null, 9)]
    public void ResolveAfter_PrefersHeaderThenQueryThenZero(string? header, string? query, long expected) =>
        Assert.Equal(expected, ServerSentEvents.ResolveAfter(header, query));

    [Fact]
    public void Keepalive_IsACommentLine() {
        Assert.StartsWith(":", ServerSentEvents.Keepalive, StringComparison.Ordinal);
        Assert.EndsWith("\n\n", ServerSentEvents.Keepalive, StringComparison.Ordinal);
    }
}
