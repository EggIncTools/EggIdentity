using System.Collections.Immutable;

namespace EggIdentity.Styles.Components;

internal static class Modals {
    internal static readonly ImmutableDictionary<string, string> Applies = new Dictionary<string, string> {
        { ".modal-backdrop", "fixed inset-0 z-[50] flex items-center justify-center overflow-y-auto p-4 [background:rgba(0,0,0,.6)] [backdrop-filter:blur(4px)]" },
        { ".modal-card", "bg-panel border border-border rounded-lg flex flex-col overflow-hidden shadow-[0_20px_25px_-5px_rgba(0,0,0,.4)] [width:var(--modal-card-w,100%)] [max-width:var(--modal-card-max,48rem)] [max-height:var(--modal-card-max-h,calc(100dvh_-_2rem))]" },
        { ".modal-card.modal-card-sm", "max-w-lg" },
        { ".modal-card.modal-card-lg", "max-w-5xl" },
        { ".modal-head", "flex items-center justify-between gap-3 px-5 py-3 border-b border-border shrink-0 bg-panel0" },
        { ".modal-title", "text-base font-semibold m-0 text-fg" },
        { ".modal-body", "px-5 py-4 flex-1 min-h-0 overflow-y-auto" },
    }.ToImmutableDictionary();
}
