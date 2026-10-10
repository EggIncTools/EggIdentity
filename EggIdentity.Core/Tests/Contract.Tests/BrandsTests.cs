namespace EggIdentity.Contract.Tests;

public class BrandsTests {
    [Theory]
    [InlineData(1, 16)]
    [InlineData(16, 16)]
    [InlineData(17, 32)]
    [InlineData(64, 64)]
    [InlineData(200, 256)]
    [InlineData(512, 512)]
    [InlineData(4096, 512)]
    public void NearestIconSize_roundsUpAndClampsToLargest(int desired, int expected) =>
        Assert.Equal(expected, Brands.NearestIconSize(desired));

    [Fact]
    public void Find_isOrdinalAndReturnsNullForUnknown() {
        Assert.Equal("EggLedger", Brands.Find("ledger")!.DisplayName);
        Assert.Null(Brands.Find("Ledger"));
        Assert.Null(Brands.Find("nonsense"));
        Assert.Null(Brands.Find(null));
    }

    [Fact]
    public void EverySlugIsUniqueAndHasAnIcon() {
        Assert.Equal(Brands.All.Length, Brands.All.Select(b => b.Slug).Distinct(StringComparer.Ordinal).Count());
        Assert.All(Brands.All, b => Assert.True(b.Has(BrandFamily.Icon)));
    }

    [Fact]
    public void HasMatchesDeclaredFamilies() {
        var incognito = Brands.Find("incognito")!;
        Assert.True(incognito.Has(BrandFamily.Lockup));
        Assert.True(incognito.Has(BrandFamily.Wordmark));

        var ledger = Brands.Find("ledger")!;
        Assert.False(ledger.Has(BrandFamily.Lockup));
        Assert.False(ledger.Has(BrandFamily.Wordmark));
    }

    [Fact]
    public void TrimInsetIsUniformAndKeepsTheIconSquare() {
        var incognito = Brands.Find("incognito")!;
        Assert.Equal(40, incognito.UniformTrimInset);
        Assert.Equal(432, 512 - (incognito.UniformTrimInset * 2));
    }

    [Fact]
    public void PathsUseTheContentPrefix() {
        Assert.Equal("_content/EggIdentity.Brand/brand/ledger/icon-64.png", Brands.IconPath("ledger", 64));
        Assert.Equal("_content/EggIdentity.Brand/brand/incognito/lockup.png", Brands.LockupPath("incognito"));
        Assert.Equal("_content/EggIdentity.Brand/brand/tools/tools.ico", Brands.FaviconPath("tools"));
    }

    [Fact]
    public void OnlyToolsShipsAFavicon() =>
        Assert.Equal(["tools"], Brands.All.Where(b => b.HasFavicon).Select(b => b.Slug));
}
