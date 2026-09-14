using EggIdentity.Settings;

namespace EggIdentity.Deploy.Tests;

public class PromotionTests {
    private static DeployApp Prod(string tag, string previous = "") => new() {
        Name = "eggledger",
        Repository = "ghcr.io/egginctools/eggledger",
        Tag = tag,
        PreviousTag = previous,
        Environment = DeployApp.ProdEnvironment,
    };

    private static DeployApp SubProd(string tag = "latest") => new() {
        Name = "eggledger",
        Repository = "ghcr.io/egginctools/eggledger",
        Tag = tag,
        Environment = DeployApp.SubProdEnvironment,
    };

    [Fact]
    public void LegacyImageRow_StillBinds() {
        var app = CollectionBinder.Bind<DeployApp>(new Dictionary<string, string?> {
            ["name"] = "eggledger",
            ["image"] = "ghcr.io/egginctools/eggledger:latest",
        });

        Assert.Equal("ghcr.io/egginctools/eggledger:latest", app.Image);
        Assert.Equal("latest", app.ResolvedTag);
    }

    [Fact]
    public void LegacyImageWithoutTag_DefaultsToLatest() {
        var app = CollectionBinder.Bind<DeployApp>(new Dictionary<string, string?> {
            ["name"] = "eggledger",
            ["image"] = "ghcr.io/egginctools/eggledger",
        });

        Assert.Equal("ghcr.io/egginctools/eggledger:latest", app.Image);
    }

    [Fact]
    public void LegacyDigestPin_SurvivesRoundTrip() {
        var app = CollectionBinder.Bind<DeployApp>(new Dictionary<string, string?> {
            ["name"] = "eggledger",
            ["image"] = "ghcr.io/egginctools/eggledger@sha256:abc",
        });

        Assert.Equal("ghcr.io/egginctools/eggledger@sha256:abc", app.Image);
    }

    [Fact]
    public void ExplicitRepositoryAndTag_WinOverLegacyImage() {
        var app = CollectionBinder.Bind<DeployApp>(new Dictionary<string, string?> {
            ["name"] = "eggledger",
            ["image"] = "ghcr.io/egginctools/eggledger:latest",
            ["repository"] = "ghcr.io/egginctools/eggledger",
            ["tag"] = "v2.4.3",
        });

        Assert.Equal("ghcr.io/egginctools/eggledger:v2.4.3", app.Image);
    }

    [Fact]
    public void Promote_MovesSubProdTagIntoProd_AndRemembersPrevious() {
        var plan = Promotion.Plan([SubProd("v2.4.4"), Prod("v2.4.3")], "eggledger");

        Assert.NotNull(plan);
        Assert.Equal("v2.4.3", plan.FromTag);
        Assert.Equal("v2.4.4", plan.ToTag);
        Assert.False(plan.IsNoOp);

        var promoted = Promotion.Apply(plan);
        Assert.Equal("v2.4.4", promoted.ResolvedTag);
        Assert.Equal("v2.4.3", promoted.PreviousTag);
        Assert.Equal("ghcr.io/egginctools/eggledger:v2.4.4", promoted.Image);
    }

    [Fact]
    public void PromotingTheSameTagTwice_ChangesNothing() {
        var plan = Promotion.Plan([SubProd("v2.4.3"), Prod("v2.4.3")], "eggledger");

        Assert.NotNull(plan);
        Assert.True(plan.IsNoOp);
        Assert.Same(plan.Target, Promotion.Apply(plan));
    }

    [Fact]
    public void RollBack_ReturnsToPreviousTag_AndIsItselfReversible() {
        var promoted = Promotion.Apply(Promotion.Plan([SubProd("v2.4.4"), Prod("v2.4.3")], "eggledger")!);
        Assert.True(promoted.CanRollBack);

        var rolled = promoted.RollBack();
        Assert.Equal("v2.4.3", rolled.ResolvedTag);
        Assert.Equal("v2.4.4", rolled.PreviousTag);
    }

    [Fact]
    public void RollBack_WithNoHistory_IsANoOp() {
        var app = Prod("v2.4.3");

        Assert.False(app.CanRollBack);
        Assert.Same(app, app.RollBack());
    }

    [Fact]
    public void Plan_NeedsBothEnvironments() {
        Assert.Null(Promotion.Plan([Prod("v2.4.3")], "eggledger"));
        Assert.Null(Promotion.Plan([SubProd()], "eggledger"));
        Assert.Null(Promotion.Plan([SubProd(), Prod("v1")], "eggincognito"));
    }

    [Fact]
    public void Row_CarriesOnlyTheTagColumns() {
        var promoted = Promotion.Apply(Promotion.Plan([SubProd("v2.4.4"), Prod("v2.4.3")], "eggledger")!);
        var row = Promotion.Row(promoted);

        Assert.Equal("v2.4.4", row["tag"]);
        Assert.Equal("v2.4.3", row["previous_tag"]);
        Assert.Equal(2, row.Count);
    }

    [Fact]
    public void SubProdTracksLatest_ProdDoesNot() {
        Assert.True(SubProd().TracksLatest);
        Assert.False(Prod("v2.4.3").TracksLatest);
    }
}
