using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Calendar {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".cal-viewport", "relative flex-1 min-h-[20rem] overflow-hidden [overscroll-behavior:contain] [touch-action:none] border-t border-border" },
        { ".cal-strip", "absolute inset-x-0 [top:var(--strip-top,-100%)] [height:var(--strip-h,300%)] flex flex-col" },
        { ".cal-period", $"[height:var(--strip-slot-h,calc(100%/3))] shrink-0 flex flex-col [transition:opacity_{Motion.Base}_{Motion.Standard}]" },
        { ".cal-canvas", "flex flex-col flex-1 min-h-0 overflow-y-auto border-b border-border" },
        { ".cal-row", $"relative flex-1 min-h-0 overflow-hidden border-b border-border [transition:opacity_{Motion.Base}_{Motion.Standard}] flex flex-col" },
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
        { ".cal-range", "flex flex-wrap items-center gap-2 min-w-0" },
        { ".cal-range-label", "text-sm font-semibold text-fg whitespace-nowrap" },
        { ".cal-range-nav", "inline-flex items-center gap-1" },
        { ".cal-range-date", "w-auto h-7 box-border text-xs py-0 px-2" },
    }.ToImmutableDictionary();
}
