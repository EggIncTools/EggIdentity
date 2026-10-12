using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Filters {
    private const string Fast = $"{Motion.Fast}_{Motion.Standard}";
    private const string SelectBg = "var(--filter-select-bg,var(--color-bg))";
    private const string SelectFg = "var(--filter-select-fg,var(--color-fg))";
    private const string Incomplete = "var(--filter-incomplete-color,var(--color-warn))";
    private const string OrColor = "var(--filter-or-color,var(--color-accent2))";
    private const string Muted = "var(--color-muted)";

    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".filter-panel", "flex flex-col gap-2" },
        { ".filter-bucket", "flex flex-col gap-1" },
        { ".filter-row", "flex flex-row flex-wrap items-center gap-2 py-1" },
        { ".filter-glue", "flex items-center gap-2 text-[0.65rem] font-bold tracking-wide [color:var(--color-muted)]" },
        { ".filter-glue.filter-glue-outer","[color:var(--color-accent)]" },
        { ".filter-glue.filter-glue-inner","[color:var(--color-muted)]" },
        { ".filter-warn", "inline-flex items-center [color:var(--color-warn)] cursor-help" },
        { ".filter-add-btn", "inline-flex items-center gap-1 self-start h-7 px-2 rounded-md border text-[0.7rem] tracking-wide cursor-pointer [color:var(--color-muted)] [border-color:var(--color-border)]" },
        { ".filter-add-btn.filter-add-inner","[color:var(--color-accent2)] [border-color:var(--color-accent2)]" },
        { ".filter-add-btn.filter-add-outer","[border-style:dashed]" },
        { ".filter-remove-btn", "inline-flex items-center justify-center w-5 h-5 rounded [color:var(--color-err)] [background-color:transparent] [border:0] cursor-pointer" },
        { ".filter-remove-btn:hover", "[background-color:color-mix(in_srgb,var(--color-err)_15%,transparent)]" },
        { ".filter-field", "relative flex items-center" },
        { ".filter-field > input", "w-full pl-7" },
        { ".filter-glass", "absolute left-2 [color:var(--color-muted)] pointer-events-none" },

        { ".fb", "flex flex-col [gap:var(--filter-row-gap,0.25rem)] [container-type:inline-size]" },
        { ".fb-divider", "flex items-center gap-2 font-bold [color:var(--filter-glue-color,var(--color-muted))] [font-size:var(--filter-divider-size,0.65rem)] [letter-spacing:var(--filter-divider-tracking,0.1em)]" },
        { ".fb-divider::before", "[content:''] flex-1 [border-top:var(--filter-divider-line,1px_solid_var(--color-border))]" },
        { ".fb-divider::after", "[content:''] flex-1 [border-top:var(--filter-divider-line,1px_solid_var(--color-border))]" },
        { ".fb-row", "flex flex-row flex-nowrap items-center [gap:var(--filter-row-gap,0.5rem)] [padding:var(--filter-row-padding,0.25rem_0)]" },
        { ".fb-row.fb-or", "[padding-left:var(--filter-or-indent,1.25rem)]" },
        { ".fb-chip", "inline-flex items-center px-1.5 font-bold [background-color:var(--filter-chip-bg,transparent)] [color:var(--filter-chip-fg,var(--color-accent2))] [border:var(--filter-chip-border,1px_solid_color-mix(in_srgb,var(--color-accent2)_60%,transparent))] [border-radius:var(--filter-chip-radius,9999px)] [font-size:var(--filter-chip-size,0.6rem)]" },
        { ".fb-field", "min-w-0 [flex:0_0_var(--filter-field-w,11rem)]" },
        { ".fb-op", "min-w-0 [flex:0_0_var(--filter-op-w,9rem)]" },
        { ".fb-value", "min-w-0 flex-1" },
        { ".fb-value > *", "w-full" },
        { ".fb-select", $"[background-color:{SelectBg}] [color:{SelectFg}] [border:var(--filter-select-border,1px_solid_var(--color-border))] [border-radius:var(--filter-select-radius,0.375rem)] [padding:var(--filter-select-padding,0.375rem_0.5rem)] [font-size:var(--filter-select-size,0.875rem)] [transition:background-color_{Fast},border-color_{Fast},box-shadow_{Fast}]" },
        { ".fb-select:hover", "[background-color:var(--filter-select-hover-bg,var(--color-panel))]" },
        { ".fb-select:focus-visible", "[outline:none] [box-shadow:var(--filter-focus-ring,0_0_0_1px_var(--color-accent))] [border-color:var(--color-accent)]" },
        { ".fb-select:disabled", "[opacity:var(--filter-disabled-opacity,0.6)] cursor-not-allowed" },
        { ".fb-select option", "[background-color:var(--filter-option-bg,var(--color-bg))] [color:var(--filter-option-fg,var(--color-fg))]" },
        { ".fb-is", $"[color:var(--filter-is-color,{Muted})] [font-size:var(--filter-select-size,0.875rem)]" },
        { ".fb-incomplete", $"[border-color:{Incomplete}] [box-shadow:var(--filter-incomplete-glow,0_0_5px_1px_color-mix(in_srgb,var(--filter-incomplete-color,var(--color-warn))_55%,transparent))]" },
        { ".fb-warn", $"inline-flex items-center cursor-help [color:{Incomplete}]" },
        { ".fb-btn", $"inline-flex items-center justify-center cursor-pointer [height:var(--filter-btn-size,1.9rem)] [min-width:var(--filter-btn-size,1.9rem)] [border-radius:var(--filter-btn-radius,0.375rem)] [font-size:var(--filter-btn-font-size,0.75rem)] [transition:background-color_{Fast},color_{Fast}]" },
        { ".fb-btn.fb-or-btn", $"px-2 font-bold [color:{OrColor}] [border:var(--filter-or-border,1px_solid_color-mix(in_srgb,var(--color-accent2)_70%,transparent))] [background-color:transparent]" },
        { ".fb-btn.fb-or-btn:hover", "[background-color:var(--filter-or-hover-bg,color-mix(in_srgb,var(--color-accent2)_15%,transparent))]" },
        { ".fb-btn.fb-or-btn:disabled", "[opacity:var(--filter-or-disabled-opacity,0.4)] cursor-not-allowed" },
        { ".fb-btn.fb-remove", "[color:var(--filter-remove-fg,var(--color-muted))] [border:1px_solid_var(--filter-remove-border,var(--color-border))] [background-color:transparent]" },
        { ".fb-btn.fb-remove:hover", "[color:var(--filter-remove-hover-fg,var(--color-err))]" },
        { ".fb-readonly .fb-row", "[padding-left:0]" },
        { ".fb-readonly-value", $"[color:{SelectFg}] [font-size:var(--filter-select-size,0.875rem)]" },
        { ".fb-footer", "flex items-center gap-2 pt-1" },
    }.ToImmutableDictionary();
}
