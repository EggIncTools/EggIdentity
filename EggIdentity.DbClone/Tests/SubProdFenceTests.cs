namespace EggIdentity.DbClone.Tests;

public class SubProdFenceTests {
    private static readonly SubProdFence Fence = new([
        new FenceGate("discord", ["DISCORD_TOKEN"]),
        new FenceGate("deploy", ["DEPLOY_AGENT_URL", "DEPLOY_SECRET"]),
    ]);

    private static Func<string, string?> Env(params (string Key, string Value)[] pairs) {
        var map = pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return k => map.GetValueOrDefault(k);
    }

    [Fact]
    public void Wrap_BlanksFencedKeysOnSubProd() {
        var get = Fence.Wrap(Env(("APP_ENVIRONMENT", "subprod"), ("DISCORD_TOKEN", "t"), ("OTHER", "o")));

        Assert.Equal("", get("DISCORD_TOKEN"));
        Assert.Equal("o", get("OTHER"));
    }

    [Fact]
    public void Wrap_OpensGateWithAllowVariable() {
        var get = Fence.Wrap(Env(("APP_ENVIRONMENT", "subprod"), ("SUBPROD_ALLOW_DISCORD", "true"), ("DISCORD_TOKEN", "t"), ("DEPLOY_SECRET", "s")));

        Assert.Equal("t", get("DISCORD_TOKEN"));
        Assert.Equal("", get("DEPLOY_SECRET"));
    }

    [Fact]
    public void Wrap_PassesThroughOnProd() {
        var get = Fence.Wrap(Env(("DISCORD_TOKEN", "t")));

        Assert.Equal("t", get("DISCORD_TOKEN"));
    }

    [Fact]
    public void Report_ListsEveryKeyWithGateState() {
        var report = Fence.Report(Env(("APP_ENVIRONMENT", "subprod"), ("SUBPROD_ALLOW_DEPLOY", "true")));

        Assert.Equal(3, report.Count);
        var discord = report.Single(r => r.EnvKey == "DISCORD_TOKEN");
        Assert.False(discord.Open);
        Assert.True(discord.Fenced);
        Assert.Equal("SUBPROD_ALLOW_DISCORD", discord.AllowKey);
        Assert.All(report.Where(r => r.Gate == "deploy"), r => Assert.False(r.Fenced));
    }

    [Fact]
    public void AllowKey_UpperCasesAndReplacesPunctuation() {
        Assert.Equal("SUBPROD_ALLOW_EGG_IDENTITY", SubProdFence.AllowKey("egg-identity"));
    }
}
