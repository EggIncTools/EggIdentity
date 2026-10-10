using System.Collections.Immutable;

namespace EggIdentity.Contract;

[Flags]
public enum BrandFamily {
    None = 0,
    Icon = 1,
    Lockup = 2,
    Wordmark = 4,
}

public sealed record BrandInfo {
    public string Slug { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Accent { get; init; } = "";
    public BrandFamily Families { get; init; } = BrandFamily.Icon;
    public int UniformTrimInset { get; init; }
    public bool HasFavicon { get; init; }

    public bool Has(BrandFamily family) => (Families & family) == family;
}

public static class Brands {
    public const string AssetBase = "_content/EggIdentity.Brand/brand";

    public static readonly ImmutableArray<int> IconSizes = [16, 32, 64, 128, 180, 192, 256, 512];

    public static readonly ImmutableArray<BrandInfo> All = [
        new BrandInfo {
            Slug = "ledger", DisplayName = "EggLedger", Accent = "#3b82f6",
        },
        new BrandInfo {
            Slug = "incognito", DisplayName = "EggIncognito", Accent = "#ef7559",
            Families = BrandFamily.Icon | BrandFamily.Lockup | BrandFamily.Wordmark,
            UniformTrimInset = 40,
        },
        new BrandInfo {
            Slug = "abacus", DisplayName = "EggAbacus", Accent = "#f43f5e",
        },
        new BrandInfo {
            Slug = "identity", DisplayName = "EggIdentity", Accent = "#f5b93b",
        },
        new BrandInfo {
            Slug = "tools", DisplayName = "EggIncTools", Accent = "#7aa2ff",
            HasFavicon = true,
        },
    ];

    private static readonly ImmutableDictionary<string, BrandInfo> BySlug =
        All.ToImmutableDictionary(b => b.Slug, StringComparer.Ordinal);

    public static BrandInfo? Find(string? slug) =>
        slug is not null && BySlug.TryGetValue(slug, out var brand) ? brand : null;

    public static string IconPath(string slug, int size) => $"{AssetBase}/{slug}/icon-{size}.png";

    public static string LockupPath(string slug) => $"{AssetBase}/{slug}/lockup.png";

    public static string WordmarkPath(string slug) => $"{AssetBase}/{slug}/wordmark.png";

    public static string FaviconPath(string slug) => $"{AssetBase}/{slug}/{slug}.ico";

    public static int NearestIconSize(int desired) {
        var best = IconSizes[0];
        foreach (var size in IconSizes) {
            if (size >= desired) return size;
            best = size;
        }
        return best;
    }
}
