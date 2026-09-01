using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Filters {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".filter-panel", "flex flex-col gap-2" },
        { ".filter-bucket", "flex flex-col gap-1" },
        { ".filter-row", "flex flex-row flex-wrap items-center gap-2 py-1" },
        { ".filter-glue", "flex items-center gap-2 text-[0.65rem] font-bold tracking-wide [color:var(--color-muted)]" },
        { ".filter-glue-outer", "[color:var(--color-accent)]" },
        { ".filter-glue-inner", "[color:var(--color-muted)]" },
        { ".filter-warn", "inline-flex items-center [color:var(--color-warn)] cursor-help" },
        { ".filter-add-btn", "inline-flex items-center gap-1 self-start h-7 px-2 rounded-md border text-[0.7rem] tracking-wide cursor-pointer [color:var(--color-muted)] [border-color:var(--color-border)]" },
        { ".filter-add-inner", "[color:var(--color-accent2)] [border-color:var(--color-accent2)]" },
        { ".filter-add-outer", "[border-style:dashed]" },
        { ".filter-remove-btn", "inline-flex items-center justify-center w-5 h-5 rounded [color:var(--color-err)] [background-color:transparent] [border:0] cursor-pointer" },
        { ".filter-remove-btn:hover", "[background-color:color-mix(in_srgb,var(--color-err)_15%,transparent)]" },
    }.ToImmutableDictionary();
}
