using EggIdentity.Bot;
using EggIdentity.Contract;
using Xunit;

namespace EggIdentity.Bot.Tests;

public class DeployAgentClientTests {
    [Fact]
    public void Parse_ReadsFrozenWire() {
        var r = DeployAgentClient.Parse(
            "{\"ok\":true,\"alreadyUpToDate\":true,\"fromHash\":\"abc1234\",\"toHash\":\"abc1234\"}");
        Assert.True(r.Ok);
        Assert.True(r.AlreadyUpToDate);
        Assert.Equal("abc1234", r.FromHash);
        Assert.Equal("abc1234", r.ToHash);
    }

    [Fact]
    public void Parse_BadJson_ReturnsTail() {
        var r = DeployAgentClient.Parse("not json");
        Assert.False(r.Ok);
        Assert.Equal("could not decode deploy agent response", r.Tail);
    }

    [Fact]
    public void Parse_DeployStatus_UpdateAvailable_MapsToInProgress() {
        var r = DeployAgentClient.Parse(
            "{\"app\":\"eggledger\",\"runningRevision\":\"aaaaaaa1111\",\"latestRevision\":\"bbbbbbb2222\",\"updateAvailable\":true,\"busy\":true,\"lastEvent\":{\"id\":3,\"app\":\"eggledger\",\"phase\":\"Pulling\",\"message\":\"pulling\",\"at\":\"2026-09-05T00:00:00Z\"}}");
        Assert.True(r.Ok);
        Assert.False(r.AlreadyUpToDate);
        Assert.Equal("aaaaaaa", r.FromHash);
        Assert.Equal("bbbbbbb", r.ToHash);
        Assert.Equal("pulling", r.Tail);
    }

    [Fact]
    public void Parse_DeployStatus_UpToDate() {
        var r = DeployAgentClient.Parse(
            "{\"app\":\"eggledger\",\"runningRevision\":\"abc1234\",\"latestRevision\":\"abc1234\",\"updateAvailable\":false,\"busy\":false}");
        Assert.True(r.Ok);
        Assert.True(r.AlreadyUpToDate);
        Assert.Equal("abc1234", r.FromHash);
    }

    [Fact]
    public void FromStatus_FailedLastEvent_IsFailure() {
        var status = new DeployStatus("x", null, null, null, null, null, null, false, null,
            new DeployEvent(1, "x", DeployPhase.Failed, "boom", DateTimeOffset.UnixEpoch, null, null, null, null), false);
        var r = DeployAgentClient.FromStatus(status);
        Assert.False(r.Ok);
        Assert.Equal("boom", r.Tail);
    }
}
