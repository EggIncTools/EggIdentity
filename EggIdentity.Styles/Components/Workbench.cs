using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Workbench {
    private const string Dur = "var(--wb-morph-dur,220ms)";
    private const string Ease = "var(--wb-morph-ease,cubic-bezier(.2,0,0,1))";
    private const string DrawerW = "var(--wb-drawer-w,min(100%,40rem))";
    private const string DrawerBorder = "var(--wb-drawer-border,var(--color-border))";

    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".modal-card.wb-card", $"flex flex-col [width:var(--wb-card-w,92vw)] [height:var(--wb-card-h,88vh)] [max-width:var(--wb-card-max,80rem)] [transition:width_{Dur}_{Ease},max-width_{Dur}_{Ease},height_{Dur}_{Ease}]" },
        { ".wb-card.wb-card-wide", "[--wb-card-w:94vw] [--wb-card-h:90vh] [--wb-card-max:92rem]" },
        { ".wb-body", "flex flex-1 min-h-0" },
        { ".wb-body:has(.wb-drawer)", "relative" },
        { ".wb-main", $"flex-1 min-w-0 overflow-y-auto p-4 [transition:margin-right_{Dur}_{Ease},padding-right_{Dur}_{Ease}]" },
        { ".wb-drawer-on .wb-main", $"[margin-right:{DrawerW}] [padding-right:var(--wb-drawer-gap,2rem)]" },
        { ".wb-drawer", $"absolute top-0 right-0 bottom-0 z-[30] flex flex-col gap-3 [width:{DrawerW}] p-3 box-border overflow-y-auto [overscroll-behavior:contain] [border-left:1px_solid_{DrawerBorder}] bg-panel [transform:translateX(100%)] invisible [transition:transform_{Dur}_{Ease},visibility_0s_linear_{Dur}]" },
        { ".wb-drawer.wb-drawer-open", $"[transform:none] visible [transition:transform_{Dur}_{Ease}]" },
        { ".wb-drawer-tab", $"absolute top-1/2 right-0 z-[31] flex flex-col items-center gap-1.5 py-2 px-1 [border:1px_solid_{DrawerBorder}] border-r-0 rounded-l-lg rounded-r-none bg-panel2 text-muted cursor-pointer [transform:translateY(-50%)] [transition:right_{Dur}_{Ease},color_.12s]" },
        { ".wb-drawer-tab:hover", "text-fg" },
        { ".wb-drawer-tab.wb-drawer-tab-open", $"[right:{DrawerW}]" },
        { ".wb-drawer-tab-label", "text-[0.625rem] font-semibold [letter-spacing:0.08em] uppercase [writing-mode:vertical-rl] [transform:rotate(180deg)] whitespace-nowrap" },
        { ".wb-pane-in", $"[animation:wb-pane-in_{Dur}_{Ease}_backwards]" },
        { ".wb-notice", "flex items-start gap-2 mb-2" },
        { ".wb-head-tools", "relative flex items-center gap-2" },
        { ".wb-rail", $"shrink-0 border-r border-border overflow-y-auto p-2 flex flex-col gap-2 [width:var(--wb-rail-w,18rem)] [scrollbar-gutter:stable] [transition:width_{Dur}_{Ease}]" },
        { ".wb-entry", "border [border-color:var(--wb-entry-border,var(--color-border))] rounded-md bg-bg px-2 py-1.5 cursor-pointer text-xs flex flex-col gap-0.5" },
        { ".wb-entry:hover", "[border-color:var(--color-accent)]" },
        { ".wb-entry:focus-visible", "[outline:1px_solid_var(--color-accent)] [outline-offset:-1px]" },
        { ".wb-entry.selected", "[border-color:var(--color-accent)] [background-color:color-mix(in_oklab,var(--color-accent)_10%,transparent)]" },
        { ".wb-entry.compare", "[border-color:var(--color-accent)] [background-color:color-mix(in_oklab,var(--color-accent)_10%,transparent)] [border-style:dashed]" },
        { ".wb-entry.tone-muted", "[opacity:0.6]" },
        { ".wb-entry.tone-warn", "[--wb-entry-border:color-mix(in_oklab,var(--color-warn)_50%,transparent)]" },
        { ".wb-entry.tone-bad", "[--wb-entry-border:var(--color-err)]" },
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
