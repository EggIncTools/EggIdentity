using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class EmptyStates {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".empty", "flex flex-col items-center justify-center gap-2 p-6 text-center text-muted" },
        { ".empty.empty-fill", "flex-1 min-h-0" },
        { ".empty.empty-tall", "min-h-[16rem]" },
        { ".empty-icon", "[--icon-size:2rem] opacity-60" },
        { ".empty-title", "text-sm font-semibold text-fg" },
        { ".empty-hint", "text-xs max-w-[28rem]" },
        { ".empty-actions", "flex flex-wrap justify-center gap-2 mt-1" },
        { ".fault", "flex flex-col items-center justify-center gap-2 text-center text-muted" },
        { ".fault-page", "flex-1 min-h-[60vh] [padding:var(--fault-page-padding,calc(var(--spacing)*12)_calc(var(--spacing)*6))]" },
        { ".fault-panel", "flex-1 min-h-0 p-6 [border:1px_dashed_var(--color-border)] [border-radius:var(--radius-md)]" },
        { ".fault-icon", "[--icon-size:var(--fault-icon-size,2.25rem)] [color:var(--fault-icon-color,var(--color-warn))]" },
        { ".fault-panel .fault-icon", "[--icon-size:1.5rem]" },
        { ".fault-title", "text-base font-semibold text-fg" },
        { ".fault-hint", "text-sm max-w-[32rem]" },
        { ".fault-actions", "flex flex-wrap justify-center gap-2 mt-1" },
        { ".fault-ref", "flex flex-wrap items-center justify-center gap-2 mt-2 text-xs" },
        { ".fault-ref-id", "[font-family:var(--font-mono)] px-1.5 py-0.5 rounded-md bg-panel2 text-fg" },
        { ".fault-detail", "w-full max-w-[48rem] text-left mt-2" },
        { ".fault-trace", "m-0 p-3 text-xs [white-space:pre-wrap] [overflow-wrap:anywhere] [font-family:var(--font-mono)] rounded-md bg-panel2 text-fg [max-height:20rem] [overflow:auto]" },
        { ".dep-notice", "flex items-start gap-3 px-3 py-2.5 text-sm rounded-md [border:1px_solid_var(--color-border)] [border-left:3px_solid_var(--color-warn)] bg-panel" },
        { ".dep-icon", "shrink-0 [--icon-size:1.1rem] [color:var(--color-warn)] mt-0.5" },
        { ".dep-body", "flex flex-col gap-0.5 flex-1 min-w-0" },
        { ".dep-title", "font-semibold text-fg" },
        { ".dep-reason", "text-muted" },
        { ".dep-hint", "text-xs text-muted" },
    }.ToImmutableDictionary();
}
