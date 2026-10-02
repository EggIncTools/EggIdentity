using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class TableScrolls {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".table-scroll", "overflow-auto [max-height:var(--table-scroll-max-h,none)] [min-height:var(--table-scroll-min-h,0)] [overscroll-behavior:contain]" },
        { ".table-scroll > .data-table", "[overflow:clip]" },
    }.ToImmutableDictionary();
}
