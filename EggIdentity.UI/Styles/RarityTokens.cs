using System.Globalization;

namespace EggIdentity.Styles;

public static class RarityTokens {
    public const int Max = 3;

    public static string Background(int rarity) => "bg-r-" + Key(rarity);

    public static string Text(int rarity) => "text-rarity-" + Key(rarity);

    public static string Border(int rarity) => "border-rarity-" + Key(rarity);

    public static string Var(int rarity) => "var(--rarity-fg-" + Key(rarity) + ")";

    private static string Key(int rarity) => (rarity is < 0 or > Max ? 0 : rarity).ToString(CultureInfo.InvariantCulture);
}
