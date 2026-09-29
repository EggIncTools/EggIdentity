using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Rarity {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".bg-r-0", "[background-color:var(--rarity-bg-0,#443e45)]" },
        { ".bg-r-1", "[background-color:var(--rarity-bg-1,#6ab6ff)] [background-image:var(--rarity-grad-1,radial-gradient(#a8dfff,#8dd5ff,#3a9dfc))]" },
        { ".bg-r-2", "[background-color:var(--rarity-bg-2,#c03fe2)] [background-image:var(--rarity-grad-2,radial-gradient(#ce81f7,#b958ed,#8819c2))]" },
        { ".bg-r-3", "[background-color:var(--rarity-bg-3,#eeab42)] [background-image:var(--rarity-grad-3,radial-gradient(#fcdd6a,_#ffdb58,_#e09143))]" },
        { ".text-rarity-0", "[color:var(--rarity-fg-0,rgb(156_163_175))]" },
        { ".text-rarity-1", "[color:var(--rarity-fg-1,#6ab6ff)]" },
        { ".text-rarity-2", "[color:var(--rarity-fg-2,#c03fe2)]" },
        { ".text-rarity-3", "[color:var(--rarity-fg-3,#eeab42)]" },
    }.ToImmutableDictionary();
}
