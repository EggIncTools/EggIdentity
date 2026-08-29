using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Toasts {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".toast-container", "fixed right-0 flex flex-col gap-2.5 z-[1000] pointer-events-none [top:calc(48px_+_10px)]" },
        { ".toast", "pointer-events-auto min-w-[240px] max-w-[360px] bg-panel2 border border-border border-l-[3px] border-r-0 px-3.5 py-2.5 cursor-pointer rounded-[6px_0_0_6px] opacity-0 [transform:translateX(100%)] [box-shadow:0_4px_14px_rgba(0,0,0,.35)] [transition:opacity_.3s_ease,transform_.3s_ease]" },
        { ".toast.show", "opacity-100 [transform:translateX(0)]" },
        { ".toast.leaving", "opacity-0 [transform:translateX(100%)]" },
        { ".toast-msg", "text-[13px] text-fg" },
        { ".toast-time", "text-[11px] text-muted mt-1 font-mono" },
        { ".toast.toast-ok", "border-l-ok" },
        { ".toast.toast-err", "border-l-err" },
        { ".toast.toast-info", "border-l-accent2" },
    }.ToImmutableDictionary();
}
