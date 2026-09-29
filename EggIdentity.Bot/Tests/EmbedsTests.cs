using EggIdentity.Contract;

namespace EggIdentity.Bot.Tests;

public class EmbedsTests {
    private static BotConfig Cfg() => new() {
        Name = "EggLedger",
        RepoUrl = "https://github.com/x/y",
        Build = new VerifyInfo { Sha256 = "deadbeef", Version = "v1.0.0", Date = "2026-06-14" },
    };

    [Fact]
    public void Verify_BlurpleWithBuildFields() {
        var e = DefaultEmbeds.Verify(Cfg());
        Assert.Equal("EggLedger Sync Server", e.Title);
        Assert.Equal(0x5865F2u, e.Color!.Value.RawValue);
        Assert.Contains(e.Fields, f => f.Name == "SHA256" && f.Value.ToString()!.Contains("deadbeef"));
        Assert.Contains(e.Fields, f => f.Name == "Version");
        Assert.Contains(e.Fields, f => f.Name == "Built" && f.Value.ToString() == "2026-06-14");
    }

    [Fact]
    public void DashboardDefault_RendersCoreFields() {
        var snapshot = new DashboardSnapshot {
            AppName = "EggLedger",
            Version = "v1.2.3",
            BuildHash = "abc1234",
            DeployStatus = "healthy",
            UptimeSince = DateTimeOffset.UtcNow,
            RepoUrl = "https://github.com/x/y",
        };

        var e = EmbedRenderer.Render(DashboardEmbedDefaults.Default, DashboardVars.Build(snapshot));

        Assert.Equal("EggLedger", e.Title);
        Assert.Contains(e.Fields, f => f.Name == "Version" && f.Value.ToString() == "v1.2.3");
        Assert.Contains(e.Fields, f => f.Name == "Status" && f.Value.ToString() == "healthy");
        Assert.Contains(e.Fields, f => f.Name == "Build" && f.Value.ToString()!.Contains("abc1234"));
        Assert.Contains(e.Fields, f => f.Name == "Repo" && f.Value.ToString()!.Contains("github.com"));
    }

    [Fact]
    public void DashboardDefault_MissingOptionalFields_OmitsThem() {
        var snapshot = new DashboardSnapshot { AppName = "EggLedger", UptimeSince = DateTimeOffset.UtcNow };

        var e = EmbedRenderer.Render(DashboardEmbedDefaults.Default, DashboardVars.Build(snapshot));

        Assert.Equal("EggLedger", e.Title);
        Assert.DoesNotContain(e.Fields, f => f.Name == "Build");
        Assert.DoesNotContain(e.Fields, f => f.Name == "Repo");
        Assert.DoesNotContain(e.Fields, f => f.Name == "Version");
    }

    [Fact]
    public void EmbedOptions_Apply_NoOverrides_ReturnsEquivalentEmbed() {
        var original = DefaultEmbeds.Verify(Cfg());
        var applied = new EmbedOptions().Apply(original);

        Assert.Equal(original.Title, applied.Title);
        Assert.Equal(original.Color, applied.Color);
        Assert.Equal(original.Fields.Length, applied.Fields.Length);
    }

    [Fact]
    public void EmbedOptions_Apply_OverridesColorAndTitle() {
        var original = DefaultEmbeds.Verify(Cfg());
        var applied = new EmbedOptions { Color = 0x00FF00, Title = "Custom Title" }.Apply(original);

        Assert.Equal("Custom Title", applied.Title);
        Assert.Equal(0x00FF00u, applied.Color!.Value.RawValue);
    }

    [Fact]
    public void EmbedOptions_Apply_AppendsExtraFieldsAfterDefaults_InOrder() {
        var original = DefaultEmbeds.Verify(Cfg());
        var options = new EmbedOptions {
            ExtraFields = new[] { ("Region", "us-east", true), ("Tier", "pro", true) },
        };
        var applied = options.Apply(original);

        var names = applied.Fields.Select(f => f.Name).ToArray();
        Assert.Equal(original.Fields.Length + 2, applied.Fields.Length);
        Assert.Equal("Region", names[^2]);
        Assert.Equal("Tier", names[^1]);
    }
}
