using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class SegmentedToggles {
    private const string Fast = $"{Motion.Fast}_{Motion.Standard}";
    private const string Darkest = "var(--color-panel0,var(--color-bg))";
    private const string Accent = "var(--view-tab-accent,var(--color-accent))";
    private const string Ratio = "color-mix(in_oklab,var(--color-ok)_calc(var(--ratio,0)_*_100%),var(--color-err))";
    private const string LeftRadius = "[border-top-left-radius:0.375rem] [border-bottom-left-radius:0.375rem]";
    private const string RightRadius = "[border-top-right-radius:0.375rem] [border-bottom-right-radius:0.375rem]";

    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".segmented", "inline-flex items-stretch" },
        { ".segmented.segmented-wrap", "flex-wrap" },
        { ".segmented > .tooltip-trigger", "inline-flex" },
        { ".segmented > * + *", "[margin-left:-1px]" },
        { ".segmented > .segmented-opt:first-child", LeftRadius },
        { ".segmented > .segmented-opt:last-child", RightRadius },
        { ".segmented > .tooltip-trigger:first-child > .segmented-opt", LeftRadius },
        { ".segmented > .tooltip-trigger:last-child > .segmented-opt", RightRadius },
        { ".segmented-opt", $"relative inline-flex items-center gap-1.5 h-7 box-border px-3 py-1 text-xs font-medium cursor-pointer [font-family:inherit] [border:1px_solid_var(--color-border)] [border-radius:0] bg-bg text-muted [transition:background-color_{Fast},color_{Fast},border-color_{Fast}]" },
        { ".segmented-opt:not(.active):not(:disabled):hover", "bg-panel2 text-fg" },
        { ".segmented-opt.active", $"z-[1] text-fg [background-color:{Darkest}] [border-color:var(--color-muted)]" },
        { ".segmented-opt:disabled", "opacity-45 cursor-not-allowed" },
        { ".segmented-opt.active:disabled", "bg-bg text-muted [border-color:var(--color-border)]" },
        { ".segmented-opt:focus-visible", "z-[2] [outline:none] [box-shadow:0_0_0_2px_var(--color-accent)]" },
        { ".segmented-opt > .icon", "[--icon-size:0.9rem] shrink-0" },
        { ".segmented-count","text-muted [font-variant-numeric:tabular-nums]" },
        { ".view-tabs", "flex gap-2 [container-type:inline-size]" },
        { ".view-tab", $"flex flex-col items-center justify-center flex-1 min-w-0 min-h-12 gap-1 text-center cursor-pointer [padding:0.4rem_1rem] rounded-lg [font-family:inherit] bg-bg text-muted [border:1px_solid_color-mix(in_oklab,{Accent}_24%,transparent)] [box-shadow:inset_0_-2px_0_0_color-mix(in_oklab,{Accent}_32%,transparent)] [transition:background-color_{Fast},color_{Fast},border-color_{Fast},box-shadow_{Fast}]" },
        { ".view-tab:not(.active):hover", $"text-fg [background-color:color-mix(in_oklab,{Accent}_8%,var(--color-bg))]" },
        { ".view-tab.active", $"[color:{Accent}] [background-color:color-mix(in_oklab,{Accent}_14%,{Darkest})] [border-color:color-mix(in_oklab,{Accent}_60%,transparent)] [box-shadow:inset_0_-2px_0_0_{Accent}]" },
        { ".view-tab:focus-visible", $"[outline:2px_solid_{Accent}] [outline-offset:2px]" },
        { ".view-tab-label", "[font-size:1.05rem] font-bold [letter-spacing:0.02em] [line-height:1.2]" },
        { ".view-tab-sub", "[font-size:0.7rem] text-muted [line-height:1] max-w-full" },
        { ".view-tab.active .view-tab-sub", $"[color:color-mix(in_oklab,{Accent}_70%,var(--color-muted))]" },
        { ".ratio-ink", $"[color:{Ratio}]" },
        { ".ratio-fill", $"[background-color:color-mix(in_oklab,{Ratio}_var(--ratio-fill-strength,22%),var(--color-panel))]" },
        { ".ratio-chip", $"inline-flex items-center gap-1 [padding:0.1rem_0.55rem] rounded-full text-xs font-semibold [font-variant-numeric:tabular-nums] [color:{Ratio}] [background-color:color-mix(in_oklab,{Ratio}_10%,{Darkest})] [border:1px_solid_color-mix(in_oklab,{Ratio}_55%,transparent)]" },
        { ".ratio-chip.ratio-chip-muted", "text-muted [border:1px_dashed_var(--color-border)] [background-color:transparent]" },
        { ".ratio-chip-label", "text-[10px] uppercase [letter-spacing:0.06em] opacity-80" },
        { ".toolbar-group", "flex flex-wrap items-center gap-2 min-w-0" },
        { ".compact-number", "inline-flex items-center gap-1 [font-variant-numeric:tabular-nums] whitespace-nowrap" },
        { ".compact-number-icon", "[width:1em] [height:1em] [--icon-size:1em] shrink-0" },
        { ".btn-mini > .icon", "[--icon-size:0.9rem] shrink-0" },
        { ".btn-mini.btn-icon", "px-1.5" },
    }.ToImmutableDictionary();
}
