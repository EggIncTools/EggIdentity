using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Workbench {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".modal-card.wb-card", "flex flex-col [width:var(--wb-card-w,92vw)] [height:var(--wb-card-h,88vh)] [max-width:var(--wb-card-max,80rem)]" },
        { ".wb-card.wb-card-wide", "[--wb-card-w:94vw] [--wb-card-h:90vh] [--wb-card-max:92rem]" },
        { ".wb-body", "flex flex-1 min-h-0" },
        { ".wb-main", "flex-1 min-w-0 overflow-y-auto p-4" },
        { ".wb-notice", "flex items-start gap-2 mb-2" },
        { ".wb-head-tools", "relative flex items-center gap-2" },
        { ".wb-rail", "shrink-0 border-r border-border overflow-y-auto p-2 flex flex-col gap-2 [width:var(--wb-rail-w,18rem)] [scrollbar-gutter:stable]" },
        { ".wb-entry", "border border-border rounded-md bg-bg px-2 py-1.5 cursor-pointer text-xs flex flex-col gap-0.5" },
        { ".wb-entry:hover", "[border-color:var(--color-accent)]" },
        { ".wb-entry:focus-visible", "[outline:1px_solid_var(--color-accent)] [outline-offset:-1px]" },
        { ".wb-entry.selected", "[border-color:var(--color-accent)] [background-color:color-mix(in_oklab,var(--color-accent)_10%,transparent)]" },
        { ".wb-entry.compare", "[border-color:var(--color-accent)] [background-color:color-mix(in_oklab,var(--color-accent)_10%,transparent)] [border-style:dashed]" },
        { ".wb-entry.tone-muted", "[opacity:0.6]" },
        { ".wb-entry.tone-warn", "[border-color:color-mix(in_oklab,var(--color-warn)_50%,transparent)]" },
        { ".wb-entry.tone-bad", "[border-color:var(--color-err)]" },
        { ".wb-entry-head", "flex items-start gap-1 min-w-0" },
        { ".wb-entry-name", "font-mono text-fg flex-1 min-w-0 [overflow-wrap:break-word] [word-break:normal] [hyphens:none] [-webkit-hyphens:none]" },
        { ".wb-entry-meta", "text-muted font-mono text-[0.7rem]" },
        { ".wb-entry-foot", "flex items-center justify-end gap-1.5 mt-0.5 min-w-0 text-muted" },
        { ".wb-sec", "flex flex-col gap-1 flex-none" },
        { ".wb-sec-head", "flex items-center gap-1.5 text-[0.6875rem] uppercase tracking-wide text-muted" },
        { ".wb-sec-tools", "ml-auto flex items-center gap-1" },
        { ".wb-sec-body", "flex flex-col gap-1.5" },
        { ".wb-scroll", "overflow-y-auto [scrollbar-gutter:stable]" },
        { ".wb-note", "text-xs text-muted px-1 py-1.5" },
        { ".wb-seg-count", "ml-1.5 [font-family:var(--font-mono)] text-muted" },
    }.ToImmutableDictionary();
}
