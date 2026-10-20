using EggIdentity.Styles;

namespace EggIdentity.Styles.Tests;

public class RarityTokensTests {
    [Theory]
    [InlineData(0, "bg-r-0", "text-rarity-0", "border-rarity-0", "var(--rarity-fg-0)")]
    [InlineData(3, "bg-r-3", "text-rarity-3", "border-rarity-3", "var(--rarity-fg-3)")]
    [InlineData(-1, "bg-r-0", "text-rarity-0", "border-rarity-0", "var(--rarity-fg-0)")]
    [InlineData(9, "bg-r-0", "text-rarity-0", "border-rarity-0", "var(--rarity-fg-0)")]
    public void TokensClampToTheKnownSet(int rarity, string bg, string text, string border, string var) {
        Assert.Equal(bg, RarityTokens.Background(rarity));
        Assert.Equal(text, RarityTokens.Text(rarity));
        Assert.Equal(border, RarityTokens.Border(rarity));
        Assert.Equal(var, RarityTokens.Var(rarity));
    }

    [Fact]
    public void BorderClassesReadTheForegroundToken() {
        for (var r = 0; r <= 3; r++) {
            var applies = ComponentClasses.All[".border-rarity-" + r];
            Assert.Contains("--rarity-fg-" + r, applies);
        }
    }
}
