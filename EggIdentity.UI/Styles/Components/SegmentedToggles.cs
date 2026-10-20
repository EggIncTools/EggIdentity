using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class SegmentedToggles {
    private const string Fast = $"{Motion.Fast}_{Motion.Standard}";

    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".segmented", "inline-flex border border-border rounded-lg overflow-hidden" },
        { ".segmented.segmented-wrap", "flex-wrap" },
        { ".segmented > .tooltip-trigger", "inline-flex" },
        { ".segmented > * + *", "border-0 border-l border-solid border-border" },
        { ".segmented-opt", $"inline-flex items-center gap-1.5 bg-panel2 text-muted border-0 px-[0.9rem] py-[0.4rem] cursor-pointer text-[13px] [font:inherit] [transition:background-color_{Fast},color_{Fast}]" },
        { ".segmented-opt.active", "bg-accent text-bg font-semibold" },
        { ".segmented-opt:disabled", "opacity-45 cursor-not-allowed" },
        { ".segmented-opt:focus-visible", "[outline:none] [box-shadow:inset_0_0_0_2px_var(--color-accent)]" },
        { ".segmented-count", "text-[11px] [font-variant-numeric:tabular-nums] opacity-70" },
        { ".view-tabs", "flex [container-type:inline-size] border-b border-border" },
        { ".view-tab", $"flex flex-col items-start justify-center gap-0.5 flex-1 min-w-0 text-left cursor-pointer px-4 py-2.5 bg-transparent text-muted border-0 [border-bottom:2px_solid_transparent] [font:inherit] [transition:background-color_{Fast},border-color_{Fast},color_{Fast}]" },
        { ".view-tab:hover", "text-fg" },
        { ".view-tab.active", "text-fg [border-bottom-color:var(--view-tab-accent,var(--color-accent))] [background-color:color-mix(in_oklab,var(--view-tab-accent,var(--color-accent))_14%,var(--color-panel))]" },
        { ".view-tab:focus-visible", "[outline:none] [box-shadow:inset_0_0_0_2px_var(--view-tab-accent,var(--color-accent))]" },
        { ".view-tab-label", "text-sm font-bold" },
        { ".view-tab-sub", "text-xs max-w-full" },
        { ".ratio-ink", "[color:color-mix(in_oklab,var(--color-ok)_calc(var(--ratio,0)_*_100%),var(--color-err))]" },
        { ".ratio-fill", "[background-color:color-mix(in_oklab,color-mix(in_oklab,var(--color-ok)_calc(var(--ratio,0)_*_100%),var(--color-err))_var(--ratio-fill-strength,22%),var(--color-panel))]" },
        { ".ratio-chip", "inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold [font-variant-numeric:tabular-nums] [border:1px_solid_color-mix(in_oklab,var(--color-ok)_calc(var(--ratio,0)_*_100%),var(--color-err))]" },
        { ".ratio-chip.ratio-chip-muted", "[border:1px_dashed_var(--color-border)] [background-color:transparent]" },
        { ".ratio-chip-label", "text-[10px] uppercase [letter-spacing:0.06em] opacity-80" },
        { ".toolbar-group", "flex flex-wrap items-center gap-2 min-w-0" },
        { ".btn-mini > .icon", "[--icon-size:0.9rem] shrink-0" },
        { ".btn-mini.btn-icon", "px-1.5" },
    }.ToImmutableDictionary();
}
