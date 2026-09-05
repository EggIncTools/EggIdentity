using EggIdentity.Deploy;
using EggIdentity.Settings;

namespace EggIdentity.Agent.Tests;

public class AppCatalogTests {
    private static SettingsSnapshot Snapshot(params CollectionRow[] rows) {
        var registry = new SettingsRegistry([], [DeployApps.Provider]);
        return new SettingsSnapshot(registry, new Dictionary<string, string?>(), null, _ => null,
            new Dictionary<string, IReadOnlyList<CollectionRow>> { [DeployApps.Key] = rows });
    }

    private static CollectionRow Row(string name, string image, bool enabled = true, string? container = null, string? secret = null) =>
        new(DeployApps.Key, name, new Dictionary<string, string?>(StringComparer.Ordinal) {
            ["name"] = name,
            ["image"] = image,
            ["container"] = container,
            ["deploy_secret"] = secret,
            ["enabled"] = enabled ? "true" : "false",
        }, DateTimeOffset.UnixEpoch, null);

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
