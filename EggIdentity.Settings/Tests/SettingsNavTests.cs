namespace EggIdentity.Settings.Tests;

public class SettingsNavTests {
    private static SettingDescriptor In(string category, string key) =>
        new(key, key.ToUpperInvariant(), key, category, SettingKind.Text, ApplyTier.Live, Sensitivity.Plain);

    private static IReadOnlyList<SettingDescriptor> Many(string category, int count) =>
        [.. Enumerable.Range(0, count).Select(i => In(category, $"{category}.{i}"))];

    [Fact]
    public void PrefixedCategories_CollapseIntoOneGroup() {
        var nav = SettingsNav.Build([
            .. Many("Devices: capture", 4),
            .. Many("Devices: sync", 3),
            .. Many("Devices: virtual", 5),
        ]);

        var group = Assert.Single(nav);
        Assert.Equal("Devices", group.Key);
        Assert.Equal(["capture", "sync", "virtual"], group.Categories.Select(c => c.Label));
        Assert.Equal(12, group.Count);
        Assert.False(group.IsLeaf);
    }

    [Fact]
    public void SingletonCategories_FoldIntoOverflow() {
        var nav = SettingsNav.Build([
            .. Many("Core", 6),
            .. Many("Build", 1),
            .. Many("Deploy", 1),
            .. Many("Storage", 1),
        ]);

        Assert.Equal(["Core", SettingsNav.OverflowKey], nav.Select(g => g.Key));

        var overflow = nav.Single(g => g.Key == SettingsNav.OverflowKey);
        Assert.Equal(["Build", "Deploy", "Storage"], overflow.Categories.Select(c => c.Label));
        Assert.Equal(3, overflow.Count);
    }

    [Fact]
    public void OverflowKeepsCategoryIdentity_SoSelectionStillResolves() {
        var nav = SettingsNav.Build([.. Many("Core", 6), .. Many("Build", 1), .. Many("Deploy", 1)]);
        var overflow = nav.Single(g => g.Key == SettingsNav.OverflowKey);

        Assert.Equal(["Build", "Deploy"], overflow.Categories.Select(c => c.Category));
        Assert.True(overflow.Covers("Build"));
        Assert.False(overflow.Covers("Core"));
    }

    [Fact]
    public void OneSmallCategoryAlone_IsNotFolded() {
        var nav = SettingsNav.Build([.. Many("Core", 6), .. Many("Build", 1)]);

        Assert.Equal(["Build", "Core"], nav.Select(g => g.Key));
        Assert.DoesNotContain(nav, g => g.Key == SettingsNav.OverflowKey);
    }

    [Fact]
    public void GroupedCategory_IsNeverFoldedEvenWhenSmall() {
        var nav = SettingsNav.Build([
            .. Many("Devices: fake", 1),
            .. Many("Devices: sync", 1),
            .. Many("Web", 1),
            .. Many("Egress", 1),
        ]);

        var devices = nav.Single(g => g.Key == "Devices");
        Assert.Equal(2, devices.Categories.Count);

        var overflow = nav.Single(g => g.Key == SettingsNav.OverflowKey);
        Assert.Equal(["Egress", "Web"], overflow.Categories.Select(c => c.Label));
    }

    [Fact]
    public void TheFirstGroupIsNeverTheOverflow_SoOpeningThePaneNeverLandsInsideIt() {
        var nav = SettingsNav.Build([
            .. Many("Core", 6),
            .. Many("Build", 1),
            .. Many("Deploy", 1),
            .. Many("Storage", 1),
        ]);

        Assert.NotEqual(SettingsNav.OverflowKey, nav[0].Key);
        Assert.Equal("Core", nav[0].Categories[0].Category);
    }

    [Fact]
    public void UnprefixedCategory_KeepsItsOwnName() {
        Assert.Equal("Core", SettingsNav.GroupOf("Core"));
        Assert.Equal("Core", SettingsNav.LabelOf("Core"));
        Assert.Equal("Devices", SettingsNav.GroupOf("Devices: capture"));
        Assert.Equal("capture", SettingsNav.LabelOf("Devices: capture"));
    }
}
