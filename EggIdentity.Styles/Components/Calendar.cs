using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Calendar {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".cal-viewport", "relative flex-1 min-h-[20rem] overflow-hidden [overscroll-behavior:contain] [touch-action:none] border-t border-border" },
        { ".cal-strip", "absolute inset-x-0 [top:var(--strip-top,-100%)] [height:var(--strip-h,300%)] flex flex-col" },
        { ".cal-period", "[height:var(--strip-slot-h,calc(100%/3))] shrink-0 flex flex-col" },
        { ".cal-canvas", "flex flex-col flex-1 min-h-0 overflow-y-auto border-b border-border" },
        { ".cal-row", "relative flex-1 min-h-0 overflow-hidden border-b border-border [transition:opacity_.18s_ease-out] flex flex-col" },
        { ".cal-row.cal-row-fixed", "grow shrink-0 [flex-basis:var(--row-h,3.05rem)] [min-height:var(--row-h,3.05rem)]" },
        { ".cal-row.cal-row-context", "opacity-[0.55]" },
        { ".cal-period-context", "opacity-[0.55]" },
        { ".cal-cell-label", "absolute top-[0.15rem] pl-[0.35rem] text-[0.65rem] text-muted pointer-events-none z-[5]" },
        { ".cal-cell-label.cal-cell-muted", "opacity-40" },
        { ".cal-gridline", "absolute inset-y-0 w-px [background-color:color-mix(in_srgb,var(--color-border)_40%,transparent)]" },
        { ".cal-hour-tick", "absolute inset-y-0 w-px [background-color:color-mix(in_srgb,var(--color-border)_20%,transparent)]" },
        { ".cal-now", "absolute inset-y-0 w-0.5 bg-err z-[1]" },
        { ".cal-lane-group", "flex flex-col" },
        { ".cal-lane-group + .cal-lane-group", "mt-2" },
        { ".cal-lane-group:last-child", "flex-1 min-h-0" },
        { ".cal-lane", "relative flex-1 min-h-0" },
        { ".cal-range-trigger", "inline-flex items-center gap-1.5 px-3 py-[0.2rem] border border-border rounded-full bg-panel2 text-muted text-[0.85rem] font-medium cursor-pointer [transition:border-color_.12s_ease,background-color_.12s_ease,color_.12s_ease]" },
        { ".cal-range-trigger:hover", "border-accent2 text-fg" },
        { ".cal-range-panel", "fixed inset-auto top-24 left-1/2 -translate-x-1/2 m-0 w-60 max-w-[90vw] px-3 py-3 text-sm text-muted bg-panel2 border border-border rounded-md flex-col gap-2 shadow-[0_6px_18px_rgba(0,0,0,.4)]" },
        { ".cal-range-panel:popover-open", "flex" },
    }.ToImmutableDictionary();
}
