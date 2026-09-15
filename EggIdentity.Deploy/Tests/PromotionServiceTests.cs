using EggIdentity.Settings;

namespace EggIdentity.Deploy.Tests;

public class PromotionServiceTests {
    private sealed class FakeStore(params CollectionRow[] rows) : IPromotionStore {
        private readonly List<CollectionRow> _rows = [.. rows];

        public List<(string Id, IReadOnlyDictionary<string, string?> Values)> Writes { get; } = [];

        public Task<IReadOnlyList<CollectionRow>> GetRowsAsync(string collectionKey, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionRow>>(_rows);

        public Task<string?> SaveRowAsync(
            string collectionKey, string id, IReadOnlyDictionary<string, string?> values, string? updatedBy,
            CancellationToken ct = default) {
            Writes.Add((id, values));
            return Task.FromResult<string?>(null);
        }
    }

    private static CollectionRow Row(string name, string environment, string tag, params (string, string)[] extra) {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["name"] = name,
            ["environment"] = environment,
            ["repository"] = "ghcr.io/egginctools/" + name,
            ["tag"] = tag,
        };
        foreach (var (k, v) in extra) values[k] = v;
        return new CollectionRow(DeployApps.Key, name, values, DateTimeOffset.UnixEpoch, null);
    }

    [Fact]
    public async Task Promote_MovesTheProdRowToTheSubProdTag() {
        var store = new FakeStore(
            Row("eggledger", DeployApp.ProdEnvironment, "v2.5.0"),
            Row("eggledger", DeployApp.SubProdEnvironment, "latest"));

        var error = await new PromotionService(store).PromoteAsync("eggledger", "david");

        Assert.Null(error);
        Assert.Equal("latest", store.Writes[0].Values["tag"]);
        Assert.Equal("v2.5.0", store.Writes[0].Values["previous_tag"]);
    }

    [Fact]
    public async Task Promote_PreservesEveryOtherFieldOnTheRow() {
        var store = new FakeStore(
            Row("eggledger", DeployApp.ProdEnvironment, "v2.5.0",
                ("image", "ghcr.io/egginctools/eggledger:v2.5.0"),
                ("container", "eggledger-prod"),
                ("public_url", "https://eggledger.egginc.tools"),
                ("deploy_secret", "s3cret")),
            Row("eggledger", DeployApp.SubProdEnvironment, "latest"));

        await new PromotionService(store).PromoteAsync("eggledger", "david");

        var written = store.Writes[0].Values;
        Assert.Equal("ghcr.io/egginctools/eggledger:v2.5.0", written["image"]);
        Assert.Equal("eggledger-prod", written["container"]);
        Assert.Equal("https://eggledger.egginc.tools", written["public_url"]);
        Assert.Equal("s3cret", written["deploy_secret"]);
        Assert.Equal(DeployApp.ProdEnvironment, written["environment"]);
    }

    [Fact]
    public async Task Promote_WithNoSubProdRow_RefusesAndWritesNothing() {
        var store = new FakeStore(Row("eggledger", DeployApp.ProdEnvironment, "v2.5.0"));

        var error = await new PromotionService(store).PromoteAsync("eggledger", "david");

        Assert.Contains("nothing to promote from", error, StringComparison.Ordinal);
        Assert.Empty(store.Writes);
    }

    [Fact]
    public async Task Promote_WhenAlreadyOnThatTag_IsANoOp() {
        var store = new FakeStore(
            Row("eggledger", DeployApp.ProdEnvironment, "v2.5.0"),
            Row("eggledger", DeployApp.SubProdEnvironment, "v2.5.0"));

        var error = await new PromotionService(store).PromoteAsync("eggledger", "david");

        Assert.Contains("already running", error, StringComparison.Ordinal);
        Assert.Empty(store.Writes);
    }

    [Fact]
    public async Task RollBack_ReturnsToThePreviousTag() {
        var store = new FakeStore(
            Row("eggledger", DeployApp.ProdEnvironment, "latest", ("previous_tag", "v2.5.0")));

        var error = await new PromotionService(store).RollBackAsync("eggledger", "david");

        Assert.Null(error);
        Assert.Equal("v2.5.0", store.Writes[0].Values["tag"]);
        Assert.Equal("latest", store.Writes[0].Values["previous_tag"]);
    }

    [Fact]
    public async Task RollBack_WithNoPreviousTag_RefusesAndWritesNothing() {
        var store = new FakeStore(Row("eggledger", DeployApp.ProdEnvironment, "v2.5.0"));

        var error = await new PromotionService(store).RollBackAsync("eggledger", "david");

        Assert.Contains("no previous tag", error, StringComparison.Ordinal);
        Assert.Empty(store.Writes);
    }

    [Fact]
    public async Task View_ShowsBothTagsAndWhetherPromotionIsAvailable() {
        var store = new FakeStore(
            Row("eggledger", DeployApp.ProdEnvironment, "v2.5.0", ("previous_tag", "v2.4.3")),
            Row("eggledger", DeployApp.SubProdEnvironment, "latest"));

        var view = Assert.Single(await new PromotionService(store).ViewAsync());

        Assert.Equal("eggledger", view.App);
        Assert.Equal("v2.5.0", view.ProdTag);
        Assert.Equal("latest", view.SubProdTag);
        Assert.True(view.CanPromote);
        Assert.True(view.CanRollBack);
        Assert.Null(view.Blocked);
    }

    [Fact]
    public async Task View_AnAppWithNoSubProdRow_IsListedAsBlockedRatherThanHidden() {
        var store = new FakeStore(Row("eggidentity", DeployApp.ProdEnvironment, "v2.5.0"));

        var view = Assert.Single(await new PromotionService(store).ViewAsync());

        Assert.False(view.CanPromote);
        Assert.NotNull(view.Blocked);
        Assert.Contains("no sub-prod row", view.Blocked, StringComparison.Ordinal);
    }

    [Fact]
    public async Task View_DoesNotListSubProdRowsAsPromotionTargets() {
        var store = new FakeStore(
            Row("eggledger", DeployApp.ProdEnvironment, "v2.5.0"),
            Row("eggledger", DeployApp.SubProdEnvironment, "latest"));

        Assert.Single(await new PromotionService(store).ViewAsync());
    }

    [Fact]
    public async Task ALegacyRowWithOnlyAnImage_StillPromotes() {
        var legacy = new CollectionRow(
            DeployApps.Key, "eggledger",
            new Dictionary<string, string?>(StringComparer.Ordinal) {
                ["name"] = "eggledger",
                ["image"] = "ghcr.io/egginctools/eggledger:v2",
            },
            DateTimeOffset.UnixEpoch, null);
        var store = new FakeStore(legacy, Row("eggledger", DeployApp.SubProdEnvironment, "latest"));

        var error = await new PromotionService(store).PromoteAsync("eggledger", "david");

        Assert.Null(error);
        Assert.Equal("latest", store.Writes[0].Values["tag"]);
        Assert.Equal("v2", store.Writes[0].Values["previous_tag"]);
        Assert.Equal("ghcr.io/egginctools/eggledger:v2", store.Writes[0].Values["image"]);
    }
}
