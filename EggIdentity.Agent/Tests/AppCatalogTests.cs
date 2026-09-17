using System.Globalization;
using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Agent.Tests;

public class AppCatalogTests {
    private static SettingsSnapshot Snapshot(params CollectionRow[] rows) => Snapshot(rows, []);

    private static SettingsSnapshot Snapshot(IReadOnlyList<CollectionRow> rows, IReadOnlyList<CollectionRow> stacks) {
        var registry = new SettingsRegistry([], [DeployApps.Provider, DeployStacks.Provider]);
        return new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null,
            new Dictionary<string, IReadOnlyList<CollectionRow>> { [DeployApps.Key] = rows, [DeployStacks.Key] = stacks });
    }

    private static CollectionRow Row(string name, string image, bool enabled = true, string? container = null, string? secret = null,
        string environment = DeployApp.ProdEnvironment, string? stack = null) =>
        new(DeployApps.Key, name, new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["name"] = name,
            ["image"] = image,
            ["container"] = container,
            ["deploy_secret"] = secret,
            ["enabled"] = enabled ? "true" : "false",
            ["environment"] = environment,
            ["stack"] = stack,
        }, DateTimeOffset.UnixEpoch, null);

    private static CollectionRow StackRow(string name, int stackId = 56, int endpointId = 9, bool enabled = true) =>
        new(DeployStacks.Key, name, new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["name"] = name,
            ["stack_id"] = stackId.ToString(CultureInfo.InvariantCulture),
            ["endpoint_id"] = endpointId.ToString(CultureInfo.InvariantCulture),
            ["enabled"] = enabled ? "true" : "false",
        }, DateTimeOffset.UnixEpoch, null);

    [Fact]
    public void FromSnapshot_IgnoresRowsFromAnotherEnvironment() {
        var rows = Snapshot(
            Row("eggledger", "ghcr.io/x/ledger:v2"),
            Row("eggledger", "ghcr.io/x/ledger:latest", environment: DeployApp.SubProdEnvironment));

        var prod = AppCatalog.FromSnapshot(rows);
        Assert.True(prod.TryGet("eggledger", out var prodApp));
        Assert.Equal("ghcr.io/x/ledger:v2", prodApp.Image);

        var subprod = AppCatalog.FromSnapshot(rows, DeployApp.SubProdEnvironment);
        Assert.True(subprod.TryGet("eggledger", out var subApp));
        Assert.Equal("ghcr.io/x/ledger:latest", subApp.Image);
    }

    [Fact]
    public void PromotingATag_CountsAsAChangedApp() {
        var before = new AppCatalog([new DeployApp { Name = "a", Repository = "ghcr.io/x/a", Tag = "v1" }]);
        var after = new AppCatalog([new DeployApp { Name = "a", Repository = "ghcr.io/x/a", Tag = "v2" }]);

        var diff = before.DiffTo(after);
        Assert.Equal(["a"], diff.Changed.Select(a => a.Name));
        Assert.Empty(diff.Added);
        Assert.Empty(diff.Removed);
    }

    [Fact]
    public void FromSnapshot_KeepsEnabledRowsKeyedCaseInsensitively() {
        var catalog = AppCatalog.FromSnapshot(Snapshot(
            Row("EggLedger", "ghcr.io/x/ledger:latest", secret: "s3cret"),
            Row("eggincognito", "ghcr.io/x/incognito:latest", enabled: false)));

        Assert.Single(catalog.Apps);
        Assert.True(catalog.TryGet("eggledger", out var app));
        Assert.Equal("EggLedger", app.Name);
        Assert.Equal("EggLedger", app.ContainerName);
        Assert.Equal("s3cret", app.DeploySecret);
        Assert.True(app.AutoDeploy);
        Assert.False(catalog.TryGet("eggincognito", out _));
    }

    [Fact]
    public void FromSnapshot_ContainerOverridesName() {
        var catalog = AppCatalog.FromSnapshot(Snapshot(Row("eggledger", "ghcr.io/x/ledger:latest", container: "eggledger-web")));

        Assert.Equal("eggledger-web", catalog.Apps["eggledger"].ContainerName);
    }

    [Fact]
    public void DiffTo_ReportsAddedRemovedAndChanged() {
        var before = new AppCatalog([
            new DeployApp { Name = "a", Image = "img:a" },
            new DeployApp { Name = "b", Image = "img:b" },
            new DeployApp { Name = "c", Image = "img:c" },
        ]);
        var after = new AppCatalog([
            new DeployApp { Name = "a", Image = "img:a" },
            new DeployApp { Name = "b", Image = "img:b2" },
            new DeployApp { Name = "d", Image = "img:d" },
        ]);

        var diff = before.DiffTo(after);

        Assert.Equal(["d"], diff.Added.Select(x => x.Name));
        Assert.Equal(["c"], diff.Removed);
        Assert.Equal(["b"], diff.Changed.Select(x => x.Name));
        Assert.False(diff.IsEmpty);
    }

    [Fact]
    public void DiffTo_ChangedStack_IsChanged() {
        var before = new AppCatalog([new DeployApp { Name = "a", Image = "img:a", Stack = "ei-servers" }]);
        var after = new AppCatalog([new DeployApp { Name = "a", Image = "img:a", Stack = "egginc-apps" }]);

        var diff = before.DiffTo(after);

        Assert.Empty(diff.Added);
        Assert.Empty(diff.Removed);
        Assert.Equal(["a"], diff.Changed.Select(x => x.Name));
    }

    [Fact]
    public void FromSnapshot_BindsStacksAndDropsDisabledRows() {
        var catalog = AppCatalog.FromSnapshot(Snapshot(
            [Row("eggledger", "ghcr.io/x/ledger:latest", stack: "ei-servers")],
            [StackRow("ei-servers"),
             StackRow("unbound", stackId: 0, endpointId: 0),
             StackRow("egginc-apps", enabled: false)]));

        Assert.Equal("ei-servers", catalog.Apps["eggledger"].Stack);
        Assert.Equal(2, catalog.Stacks.Count);
        Assert.True(catalog.TryGetStack("EI-Servers", out var stack));
        Assert.Equal(56, stack.StackId);
        Assert.Equal(9, stack.EndpointId);
        Assert.True(stack.HasPortainerIds);
        Assert.True(catalog.TryGetStack("unbound", out var unbound));
        Assert.False(unbound.HasPortainerIds);
        Assert.False(catalog.TryGetStack("egginc-apps", out _));
    }

    [Fact]
    public void DiffTo_SameShape_IsEmpty() {
        var a = new AppCatalog([new DeployApp { Name = "a", Image = "img:a", Container = "a", DeploySecret = "x" }]);
        var b = new AppCatalog([new DeployApp { Name = "A", Image = "img:a", Container = "a", DeploySecret = "x" }]);

        Assert.True(a.DiffTo(b).IsEmpty);
    }

    [Fact]
    public void DiffTo_NameCaseChange_WithDerivedContainer_IsChanged() {
        var a = new AppCatalog([new DeployApp { Name = "a", Image = "img:a" }]);
        var b = new AppCatalog([new DeployApp { Name = "A", Image = "img:a" }]);

        var diff = a.DiffTo(b);

        Assert.Empty(diff.Added);
        Assert.Empty(diff.Removed);
        Assert.Equal(["A"], diff.Changed.Select(x => x.Name));
    }

    [Fact]
    public void DiffTo_DisabledRow_CountsAsRemoved() {
        var before = new AppCatalog([new DeployApp { Name = "a", Image = "img:a" }]);
        var after = new AppCatalog([new DeployApp { Name = "a", Image = "img:a", Enabled = false }]);

        Assert.Equal(["a"], before.DiffTo(after).Removed);
    }
}
